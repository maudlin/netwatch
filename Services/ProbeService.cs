using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Netwatch.Models;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;

namespace Netwatch.Services
{
    internal readonly record struct PingSample(long TimestampMs, bool Success, int RttMs);
    internal readonly record struct DnsSample(long TimestampMs, int ElapsedMs);

    public class ProbeService : INotifyPropertyChanged, IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly PeriodicTimer _tick = new(TimeSpan.FromSeconds(1));
        private readonly ConcurrentQueue<PingSample> _pingPublic = new();
        private readonly ConcurrentQueue<PingSample> _pingGateway = new();
        private readonly ConcurrentQueue<PingSample> _pingDns = new();
        private readonly ConcurrentQueue<DnsSample> _dnsTimes = new();

        // Streaming estimators and loss window (public ping primary)
        private readonly object _statsLock = new();
        private readonly P2QuantileEstimator _latP50 = new(0.5);
        private readonly P2QuantileEstimator _latP95 = new(0.95);
        private readonly Welford _latJitter = new();
        private readonly RingCounter _lossWindow = new(120);
        // DNS streaming estimators
        private readonly P2QuantileEstimator _dnsP50 = new(0.5);
        private readonly P2QuantileEstimator _dnsP90 = new(0.9);

        private IPAddress _publicTarget = IPAddress.Parse("1.1.1.1");
        private IPAddress? _gatewayTarget;
        private IPAddress? _dnsTarget;
        private string? _activeNicId; // NIC selected for default route (used for badge)

        private int _tickCount = 0;
        private readonly Random _rng = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string LatencyP50 { get; private set; } = "—";
        public string LatencyP95Text { get; private set; } = "p95 —";
        public string JitterMs { get; private set; } = "—";
        public string LossText { get; private set; } = "loss —";
        public bool HasRecentLoss { get; private set; } = false;
        public string LossPctOnly { get; private set; } = "—";
        public string DnsMedian { get; private set; } = "—";
        public string DnsDetail { get; private set; } = "";
        public string BloatDelta { get; private set; } = "—";
        public string BloatDetail { get; private set; } = "";
        public string LastBloatRunText { get; private set; } = "";
        public bool IsBloatTestRunning { get; private set; } = false;
        public string LinkBadge { get; private set; } = "Detecting link…";
        public string StatusText { get; private set; } = "UNKNOWN";
        public string StatusReason { get; private set; } = "";
        public string LastUpdatedText { get; private set; } = "↻ …";
        public System.Windows.Media.Brush StatusBrush { get; private set; } = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100,100,100));

        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(43,182,115));
        private static readonly SolidColorBrush AmberBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255,176,32));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229,72,77));
        static ProbeService()
        {
            GreenBrush.Freeze();
            AmberBrush.Freeze();
            RedBrush.Freeze();
        }

        public ProbeService()
        {
            RunUploadTestCommand = new RelayCommand(async _ => await RunUploadBloatTestAsync(TimeSpan.FromSeconds(10)), _ => true);
            CopySnapshotCommand = new RelayCommand(_ =>
            {
                try { System.Windows.Clipboard.SetText(BuildSnapshot()); } catch { }
            }, _ => true);
        }

        public ObservableCollection<string> TargetSummaries { get; } = new();

        public ICommand RunUploadTestCommand { get; }
        public ICommand CopySnapshotCommand { get; }

        // Time series for sparklines (last N points)
        private const int SeriesMax = 60;
        public double[] LatencySeries { get; private set; } = Array.Empty<double>();
        public double[] JitterSeries { get; private set; } = Array.Empty<double>();
        public double[] LossSeries { get; private set; } = Array.Empty<double>();
        public double[] DnsSeries { get; private set; } = Array.Empty<double>();
        public long[] LatencyTimestamps { get; private set; } = Array.Empty<long>();
        public long[] JitterTimestamps { get; private set; } = Array.Empty<long>();
        public long[] LossTimestamps { get; private set; } = Array.Empty<long>();
        public long[] DnsTimestamps { get; private set; } = Array.Empty<long>();

        // Axis overrides (sticky auto-range)
        public double? LatencyAxisMax { get; private set; } = null;
        public double? JitterAxisMax { get; private set; } = null;
        public double? LossAxisMax { get; private set; } = null;
        private readonly List<double> _latencySeries = new();
        private readonly List<double> _jitterSeries = new();
        private readonly List<double> _lossSeries = new();
        private readonly List<double> _dnsSeries = new();
        private readonly List<long> _latencyTs = new();
        private readonly List<long> _jitterTs = new();
        private readonly List<long> _lossTs = new();
        private readonly List<long> _dnsTs = new();

        public void Start()
        {
            // Subscribe to NIC change events for target re-discovery
            try { NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged; } catch { }
            _ = Task.Run(RunAsync);
        }

        private async Task RunAsync()
        {
            DiscoverTargets();
            _ = Task.Run(() => WifiPollLoop(_cts.Token));

            while (await _tick.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    _tickCount++;

                    // Handle any pending NIC changes (debounced)
                    HandlePendingNicChangeIfAny();

                    bool skipThisTickForBackoff = _offlineBackoffActive && (_tickCount % _backoffEveryNTicks != 0);

                    List<Task<bool>> pingTasks = new(3);
                    if (!skipThisTickForBackoff)
                    {
                        // Staggered pings within the 1s tick window: 0ms (gateway), 333ms (resolver), 666ms (public)
                        if (_gatewayTarget is not null) pingTasks.Add(SchedulePing(_gatewayTarget, _pingGateway, 0));
                        if (_dnsTarget is not null) pingTasks.Add(SchedulePing(_dnsTarget, _pingDns, 333));
                        pingTasks.Add(SchedulePing(_publicTarget, _pingPublic, 666));
                    }

                    // DNS timing every 12s with cache-busting prefix (still allowed during backoff, but it's infrequent)
                    if (_tickCount % 12 == 0)
                    {
                        _ = Task.Run(DoDnsTiming);
                    }

                    if (pingTasks.Count > 0)
                    {
                        await Task.WhenAll(pingTasks);
                        var anySuccess = pingTasks.Any(t => t.Status == TaskStatus.RanToCompletion && t.Result);
                        var anyAttempted = pingTasks.Count > 0;
                        if (anyAttempted && !anySuccess)
                        {
                            _consecutiveAllTimeoutsSeconds++;
                            if (_consecutiveAllTimeoutsSeconds > 10)
                            {
                                _offlineBackoffActive = true;
                            }
                        }
                        else if (anySuccess)
                        {
                            _consecutiveAllTimeoutsSeconds = 0;
                            _offlineBackoffActive = false;
                        }
                    }

                    // Trim queues to last N samples
                    Trim(_pingPublic, 120);
                    Trim(_pingGateway, 120);
                    Trim(_pingDns, 120);
                    Trim(_dnsTimes, 60);

                    // Compute metrics and raise updates (1 Hz via this loop)
                    ComputeStatus();
                }
                catch (Exception)
                {
                    // swallow per tick
                }
            }
        }

        private void DiscoverTargets()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                             .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                         (n.NetworkInterfaceType != NetworkInterfaceType.Loopback)))
                {
                    var props = nic.GetIPProperties();
                    var gw = props.GatewayAddresses.FirstOrDefault(g => g?.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (gw is not null)
                    {
                        _gatewayTarget = gw.Address;
                        _activeNicId = nic.Id;
                        break;
                    }
                }

                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                             .Where(n => n.OperationalStatus == OperationalStatus.Up))
                {
                    var props = nic.GetIPProperties();
                    var dns = props.DnsAddresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (dns is not null) { _dnsTarget = dns; break; }
                }

                var items = new List<string>
                {
                    $"Public: {_publicTarget}",
                    $"Gateway: {_gatewayTarget?.ToString() ?? "—"}",
                    $"DNS: {_dnsTarget?.ToString() ?? "—"}"
                };

                // Marshal collection updates to UI thread
                var app = System.Windows.Application.Current;
                if (app?.Dispatcher != null)
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        TargetSummaries.Clear();
                        foreach (var s in items) TargetSummaries.Add(s);
                        Raise(nameof(TargetSummaries));
                    });
                }
                else
                {
                    TargetSummaries.Clear();
                    foreach (var s in items) TargetSummaries.Add(s);
                    Raise(nameof(TargetSummaries));
                }
            }
            catch
            {
                // ignore
            }
        }

        private async Task<bool> DoPing(IPAddress addr, ConcurrentQueue<PingSample> queue)
        {
            try
            {
                using var pinger = new Ping();
                var sw = Stopwatch.StartNew();
                var reply = await pinger.SendPingAsync(addr, 900);
                sw.Stop();
                var success = reply.Status == IPStatus.Success;
                var rtt = success ? (int)reply.RoundtripTime : (int)sw.ElapsedMilliseconds;
                queue.Enqueue(new PingSample(NowMs(), success, rtt));

                // Update streaming stats for public target only
                if (addr.Equals(_publicTarget))
                {
                    lock (_statsLock)
                    {
                        _lossWindow.Add(success);
                        if (success)
                        {
                            _latP50.Add(rtt);
                            _latP95.Add(rtt);
                            _latJitter.Add(rtt);
                        }
                    }
                }
                return success;
            }
            catch
            {
                queue.Enqueue(new PingSample(NowMs(), false, 0));
                if (addr.Equals(_publicTarget))
                {
                    lock (_statsLock)
                    {
                        _lossWindow.Add(false);
                    }
                }
                return false;
            }
        }

        private readonly string[] _dnsTestHosts = new[] { "one.one.one.one", "dns.google", "example.com" };
        private int _dnsHostIndex = 0;
        private async Task DoDnsTiming()
        {
            // Cache-busting randomized prefix to avoid local resolver cache hits
            var baseHost = _dnsTestHosts[_dnsHostIndex++ % _dnsTestHosts.Length];
            var prefix = Guid.NewGuid().ToString("N").Substring(0, 8);
            var host = $"{prefix}.{baseHost}";

            var sw = Stopwatch.StartNew();
            try
            {
                var lookupTask = Dns.GetHostAddressesAsync(host);
                var timeoutTask = Task.Delay(1500);
                var done = await Task.WhenAny(lookupTask, timeoutTask);
                sw.Stop();

                int elapsed = (int)sw.ElapsedMilliseconds;
                if (done == lookupTask)
                {
                    // Record elapsed even if the DNS result is negative (exception)
                    try { await lookupTask; } catch { /* negative response (e.g., NXDOMAIN) */ }
                    _dnsTimes.Enqueue(new DnsSample(NowMs(), elapsed));
                }
                else
                {
                    // Timed out
                    elapsed = 1500;
                    _dnsTimes.Enqueue(new DnsSample(NowMs(), elapsed));
                }
                lock (_statsLock)
                {
                    _dnsP50.Add(elapsed);
                    _dnsP90.Add(elapsed);
                }
            }
            catch
            {
                sw.Stop();
                int elapsed = (int)Math.Min(1500, sw.ElapsedMilliseconds);
                _dnsTimes.Enqueue(new DnsSample(NowMs(), elapsed));
                lock (_statsLock)
                {
                    _dnsP50.Add(elapsed);
                    _dnsP90.Add(elapsed);
                }
            }
        }

        private void ComputeStatus()
        {
            // If in offline backoff and no recent successes, surface OFFLINE
            if (_offlineBackoffActive)
            {
                StatusText = "OFFLINE";
                StatusReason = "No responses from targets";
                LastUpdatedText = $"↻ {DateTime.Now:HH:mm:ss}";
            }

            double latP50, latP95, jitter, loss;
            double dnsMed, dnsP90;
            lock (_statsLock)
            {
                latP50 = _latP50.Current;
                latP95 = _latP95.Current;
                jitter = _latJitter.StdDev;
                loss = _lossWindow.LossPercent;
                dnsMed = _dnsP50.Current;
                dnsP90 = _dnsP90.Current;
            }

            if (!double.IsNaN(latP50) && _lossWindow.Count > 0)
            {
                LatencyP50 = $"{(int)Math.Round(latP50)} ms";
                LatencyP95Text = $"p95 {(int)Math.Round(latP95)} ms";
                JitterMs = $"{jitter:0} ms";
                LossText = $"loss {loss:0.0}%";
                LossPctOnly = $"{loss:0.0}%";
            }
            else
            {
                LatencyP50 = "—"; LatencyP95Text = "p95 —"; JitterMs = "—"; LossText = "loss —"; LossPctOnly = "—";
            }

            if (!double.IsNaN(dnsMed))
            {
                DnsMedian = FormatMs((int)Math.Round(dnsMed));
                DnsDetail = $"p90 {FormatMs((int)Math.Round(dnsP90))}";
            }
            else
            {
                DnsMedian = "—"; DnsDetail = "";
            }

            // Bufferbloat panel defaults
            if (string.IsNullOrEmpty(BloatDelta)) BloatDelta = "—";
            if (string.IsNullOrEmpty(BloatDetail)) BloatDetail = "Run the 10s upload test to estimate bufferbloat.";

            // Link badge (very rough)
            LinkBadge = GetActiveLinkBadge();

            // Recent loss flag over last 60s
            var nowTs = NowMs();
            HasRecentLoss = _pingPublic.Any(p => p.TimestampMs >= nowTs - 60_000 && !p.Success);

            // Status heuristic (use streaming values)
            var score = 0;
            var reason = new List<string>();

            if (!double.IsNaN(latP50))
            {
                if (loss > 2) { score += 2; reason.Add(">2% loss"); }
                else if (loss > 0.5) { score += 1; reason.Add($"{loss:0.0}% loss"); }

                if (jitter > 50) { score += 2; reason.Add($"jitter {jitter:0} ms"); }
                else if (jitter > 20) { score += 1; reason.Add($"jitter {jitter:0} ms"); }

                if (latP50 > 120) { score += 2; reason.Add($"latency {(int)Math.Round(latP50)} ms"); }
                else if (latP50 > 60) { score += 1; reason.Add($"latency {(int)Math.Round(latP50)} ms"); }
            }
            if (!double.IsNaN(dnsMed))
            {
                if (dnsMed > 150) { score += 1; reason.Add($"DNS {(int)Math.Round(dnsMed)} ms"); }
            }

            (StatusText, StatusBrush) = score switch
            {
                >=3 => ("RED", RedBrush),
                1 or 2 => ("AMBER", AmberBrush),
                _ => ("GREEN", GreenBrush),
            };
            StatusReason = reason.Count == 0 ? "Video-ready." : string.Join("; ", reason);
            LastUpdatedText = $"↻ {DateTime.Now:HH:mm:ss}";

            // Update axis upper bounds (sticky auto-range)
            UpdateAxisRanges(latP50, jitter, loss, dnsMed);

            // Update series from streaming stats
            // Use the same nowTs for timestamping series points
            if (!double.IsNaN(latP50))
            {
                AppendSeries(_latencySeries, _latencyTs, latP50, nowTs);
                AppendSeries(_jitterSeries, _jitterTs, jitter, nowTs);
                AppendSeries(_lossSeries, _lossTs, loss, nowTs);
            }
            if (!double.IsNaN(dnsMed))
            {
                AppendSeries(_dnsSeries, _dnsTs, dnsMed, nowTs);
            }
            // snapshot arrays so binding target sees a new reference each tick
            LatencySeries = _latencySeries.ToArray();
            JitterSeries = _jitterSeries.ToArray();
            LossSeries = _lossSeries.ToArray();
            DnsSeries = _dnsSeries.ToArray();
            LatencyTimestamps = _latencyTs.ToArray();
            JitterTimestamps = _jitterTs.ToArray();
            LossTimestamps = _lossTs.ToArray();
            DnsTimestamps = _dnsTs.ToArray();

            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Raise(nameof(LatencyP50));
                Raise(nameof(LatencyP95Text));
                Raise(nameof(JitterMs));
                Raise(nameof(LossText));
                Raise(nameof(LossPctOnly));
                Raise(nameof(DnsMedian));
                Raise(nameof(DnsDetail));
                Raise(nameof(BloatDelta));
                Raise(nameof(BloatDetail));
                Raise(nameof(LastBloatRunText));
                Raise(nameof(IsBloatTestRunning));
                Raise(nameof(LinkBadge));
                Raise(nameof(StatusText));
                Raise(nameof(StatusReason));
                Raise(nameof(LastUpdatedText));
                Raise(nameof(StatusBrush));
                Raise(nameof(HasRecentLoss));

                // Notify series bindings
                Raise(nameof(LatencySeries));
                Raise(nameof(JitterSeries));
                Raise(nameof(LossSeries));
                Raise(nameof(DnsSeries));
                Raise(nameof(LatencyTimestamps));
                Raise(nameof(JitterTimestamps));
                Raise(nameof(LossTimestamps));
                Raise(nameof(DnsTimestamps));
            }));
        }

        private void UpdateAxisRanges(double latP50, double jitter, double loss, double dnsMed)
        {
            try
            {
                // Helper to compute sticky upper given proposed and current
                static double StickyUpper(double proposed, double? current, double minSpan, double cap)
                {
                    // snap to nice values
                    double Snap(double v)
                    {
                        double[] nice = new double[] { 10, 20, 30, 40, 50, 75, 100, 120, 150, 200, 300, 400, 600 };
                        foreach (var n in nice)
                        {
                            if (v <= n) return n;
                        }
                        return cap;
                    }

                    proposed = Math.Max(minSpan, Math.Min(cap, proposed));
                    proposed = Snap(proposed);
                    if (current is null) return proposed;
                    double cur = current.Value;
                    // hysteresis: only change if >15% difference
                    if (Math.Abs(proposed - cur) / Math.Max(1.0, cur) > 0.15) return proposed;
                    return cur;
                }

                // Latency axis from p95 approx (use p50 and add headroom if p95 unknown)
                double latUpper = double.IsNaN(latP50) ? 100 : Math.Max(40, latP50 * 2.0);
                LatencyAxisMax = StickyUpper(latUpper * 1.25, LatencyAxisMax, 40, 300);

                // Jitter axis
                double jitUpper = double.IsNaN(jitter) ? 30 : Math.Max(10, jitter * 3.0);
                JitterAxisMax = StickyUpper(jitUpper * 1.25, JitterAxisMax, 10, 100);

                // Loss axis (percent)
                double lossUpper = double.IsNaN(loss) ? 5 : Math.Max(2, loss * 3.0);
                LossAxisMax = StickyUpper(lossUpper * 1.25, LossAxisMax, 2, 30);

                _ = System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Raise(nameof(LatencyAxisMax));
                    Raise(nameof(JitterAxisMax));
                    Raise(nameof(LossAxisMax));
                }));
            }
            catch { }
        }

        private string GetActiveLinkBadge()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToArray();

                NetworkInterface? active = null;
                if (!string.IsNullOrEmpty(_activeNicId))
                {
                    active = nics.FirstOrDefault(n => string.Equals(n.Id, _activeNicId, StringComparison.OrdinalIgnoreCase));
                }
                // Fallback: NIC that has a gateway
                if (active == null)
                {
                    active = nics.FirstOrDefault(n =>
                    {
                        try { return n.GetIPProperties().GatewayAddresses.Any(g => g?.Address.AddressFamily == AddressFamily.InterNetwork); }
                        catch { return false; }
                    });
                }
                // General fallback
                active ??= nics.FirstOrDefault();

                if (active != null)
                {
                    var type = active.NetworkInterfaceType.ToString();
                    double speedMbps = active.Speed > 0 ? (active.Speed / 1_000_000.0) : 0.0;
                    string speedText = speedMbps >= 1000 ? $"{(speedMbps / 1000.0):0.#} Gbps" : $"{speedMbps:0} Mbps";
                    return $"{type} · {speedText}";
                }
            }
            catch { }
            return "No link";
        }

        private static readonly Uri UploadEndpoint = new("https://speed.cloudflare.com/__up");

        public async Task RunUploadBloatTestAsync(TimeSpan duration)
        {
            try
            {
                IsBloatTestRunning = true;
                Raise(nameof(IsBloatTestRunning));

                // Baseline: p50 latency over the last 30 seconds (if available)
                var now = NowMs();
                var baselineWindowStart = now - 30_000;
                var baselineArr = _pingPublic.Where(p => p.Success && p.TimestampMs >= baselineWindowStart)
                                             .Select(p => p.RttMs).ToArray();
                int baselineP50 = baselineArr.Length > 0 ? PercentileOfArray(baselineArr, 50) : 0;

                // Start upload and measure during load
                using var http = new HttpClient();
                using var cts = new CancellationTokenSource(duration);

                var start = NowMs();

                // Start a temporary 2 Hz ping loop during the test to increase under-load samples
                var bloatPingTask = Task.Run(() => BloatPingLoop(cts.Token));

                var uploadTask = http.PostAsync(UploadEndpoint, new StreamContent(new InfiniteStream(cts.Token)), cts.Token);

                try { await Task.Delay(duration, cts.Token); } catch { }
                try { cts.Cancel(); } catch { }
                try { await Task.WhenAll(uploadTask, bloatPingTask); } catch { /* ignore cancellation/HTTP errors */ }

                // Under-load p50: only samples taken during the upload window
                var end = NowMs();
                var underLoadArr = _pingPublic.Where(p => p.Success && p.TimestampMs >= start && p.TimestampMs <= end)
                                              .Select(p => p.RttMs).ToArray();
                if (baselineArr.Length < 5)
                {
                    BloatDelta = "Baseline warming up—try again in ~20s";
                    BloatDetail = "Collecting idle samples for a stable baseline.";
                }
                else if (underLoadArr.Length == 0)
                {
                    BloatDelta = "No data";
                    BloatDetail = "No under-load samples collected during the test.";
                }
                else
                {
                    var loadP50 = PercentileOfArray(underLoadArr, 50);
                    var delta = Math.Max(0, loadP50 - baselineP50);
                    var sev = delta >= 100 ? "HIGH" : delta >= 40 ? "MED" : "LOW";
                    BloatDelta = $"+{delta} ms ({sev})";
                    BloatDetail = $"Idle p50 {baselineP50} ms → Load p50 {loadP50} ms (+{delta} ms {sev})";
                }
                LastBloatRunText = $"Tested at {DateTime.Now:HH:mm:ss}";
                Raise(nameof(BloatDelta));
                Raise(nameof(BloatDetail));
                Raise(nameof(LastBloatRunText));
            }
            catch
            {
                BloatDelta = "Test failed";
                BloatDetail = "Upload test encountered an error.";
                Raise(nameof(BloatDelta));
                Raise(nameof(BloatDetail));
            }
            finally
            {
                IsBloatTestRunning = false;
                Raise(nameof(IsBloatTestRunning));
            }
        }

        private async Task BloatPingLoop(CancellationToken ct)
        {
            // Aim for ~2 Hz per target: issue pings every 500 ms in parallel
            while (!ct.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                var tasks = new List<Task<bool>>(3);
                if (_gatewayTarget is not null) tasks.Add(DoPing(_gatewayTarget, _pingGateway));
                if (_dnsTarget is not null) tasks.Add(DoPing(_dnsTarget, _pingDns));
                tasks.Add(DoPing(_publicTarget, _pingPublic));
                try { await Task.WhenAll(tasks); } catch { }
                sw.Stop();
                var delay = 500 - sw.ElapsedMilliseconds;
                if (delay > 0)
                {
                    try { await Task.Delay((int)delay, ct); } catch { }
                }
            }
        }

        private static void Trim<T>(ConcurrentQueue<T> q, int max)
        {
            while (q.Count > max) q.TryDequeue(out _);
        }

        private Task<bool> SchedulePing(IPAddress addr, ConcurrentQueue<PingSample> queue, int delayMs)
        {
            return Task.Run(async () =>
            {
                try
                {
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, _cts.Token);
                    }
                    return await DoPing(addr, queue);
                }
                catch
                {
                    // ignore scheduling errors
                    return false;
                }
            });
        }

        private static void AppendSeries(List<double> series, List<long> timestamps, double value, long ts)
        {
            series.Add(value);
            timestamps.Add(ts);
            var excess = series.Count - SeriesMax;
            if (excess > 0)
            {
                series.RemoveRange(0, excess);
                timestamps.RemoveRange(0, excess);
            }
        }

        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Offline backoff tracking
        private int _consecutiveAllTimeoutsSeconds = 0;
        private bool _offlineBackoffActive = false;
        private readonly int _backoffEveryNTicks = 5; // when backoff is active, probe once every N seconds

        // NIC change handling (debounced)
        private volatile bool _nicChangePending = false;
        private DateTime _nicChangeLastSetUtc = DateTime.MinValue;
        private static readonly TimeSpan _nicDebounce = TimeSpan.FromSeconds(3);

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            _nicChangePending = true;
            _nicChangeLastSetUtc = DateTime.UtcNow;
        }

        private void HandlePendingNicChangeIfAny()
        {
            if (_nicChangePending && (DateTime.UtcNow - _nicChangeLastSetUtc) >= _nicDebounce)
            {
                _nicChangePending = false;
                // Re-discover targets; only clear buffers if targets actually changed
                var beforeGw = _gatewayTarget;
                var beforeDns = _dnsTarget;
                DiscoverTargets();
                bool changed = !Equals(beforeGw, _gatewayTarget) || !Equals(beforeDns, _dnsTarget);
                if (changed)
                {
                    ClearAllBuffersAndSeries();
                }
            }
        }

        private void ClearAllBuffersAndSeries()
        {
            // Clear queues
            while (_pingPublic.TryDequeue(out _)) { }
            while (_pingGateway.TryDequeue(out _)) { }
            while (_pingDns.TryDequeue(out _)) { }
            while (_dnsTimes.TryDequeue(out _)) { }

            // Clear series
            _latencySeries.Clear();
            _jitterSeries.Clear();
            _lossSeries.Clear();
            _dnsSeries.Clear();
            _latencyTs.Clear();
            _jitterTs.Clear();
            _lossTs.Clear();
            _dnsTs.Clear();

            // Reset counters and streaming stats
            _consecutiveAllTimeoutsSeconds = 0;
            _offlineBackoffActive = false;
            lock (_statsLock)
            {
                _latP50.Reset();
                _latP95.Reset();
                _latJitter.Reset();
                _lossWindow.Reset();
                _dnsP50.Reset();
                _dnsP90.Reset();
            }

            // Notify UI to reset visuals
            LatencySeries = Array.Empty<double>();
            JitterSeries = Array.Empty<double>();
            LossSeries = Array.Empty<double>();
            DnsSeries = Array.Empty<double>();
            LatencyTimestamps = Array.Empty<long>();
            JitterTimestamps = Array.Empty<long>();
            LossTimestamps = Array.Empty<long>();
            DnsTimestamps = Array.Empty<long>();
            _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Raise(nameof(LatencySeries));
                Raise(nameof(JitterSeries));
                Raise(nameof(LossSeries));
                Raise(nameof(DnsSeries));
                Raise(nameof(LatencyTimestamps));
                Raise(nameof(JitterTimestamps));
                Raise(nameof(LossTimestamps));
                Raise(nameof(DnsTimestamps));
            }));
        }

        // Helper for percentile on arrays for occasional computations (non-hot path)
        private static int PercentileOfArray(int[] arr, int p)
        {
            if (arr == null || arr.Length == 0) return 0;
            Array.Sort(arr);
            var rank = (p / 100.0) * (arr.Length - 1);
            var lo = (int)Math.Floor(rank);
            var hi = (int)Math.Ceiling(rank);
            if (lo == hi) return arr[lo];
            var w = rank - lo;
            return (int)(arr[lo] * (1 - w) + arr[hi] * w);
        }

        private static string FormatMs(int ms)
        {
            if (ms >= 1000)
            {
                return $"{(ms / 1000.0):0.0} s";
            }
            return $"{ms} ms";
        }

        public string BuildSnapshot()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            if (TargetSummaries.Count > 0)
                sb.AppendLine(string.Join(" | ", TargetSummaries));
            sb.AppendLine($"Link: {LinkBadge}");
            sb.AppendLine($"Ping public: {LatencyP50} / {JitterMs} / {LossText}");
            sb.AppendLine($"DNS: {DnsMedian} ({DnsDetail})");
            sb.AppendLine($"Bufferbloat: {BloatDelta}");
            sb.AppendLine($"Status: {StatusText} ({StatusReason})");
            return sb.ToString();
        }

        private async Task WifiPollLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var badge = GetActiveLinkBadge();
                    var app = System.Windows.Application.Current;
                    if (app?.Dispatcher != null)
                    {
                        _ = app.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            LinkBadge = badge;
                            Raise(nameof(LinkBadge));
                        }));
                    }
                    else
                    {
                        LinkBadge = badge;
                        Raise(nameof(LinkBadge));
                    }
                }
                catch { }
                await Task.Delay(5000, ct);
            }
        }

        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            try { NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged; } catch { }
            _cts.Cancel();
            _tick.Dispose();
        }
    }
}
