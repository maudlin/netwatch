using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using Netwatch.Services;

namespace Netwatch
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon? _tray;
        private MiniHudWindow? _mini;
        private ExpandedWindow? _expanded;
        private ProbeService? _probes;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _probes = new ProbeService();
            _probes.Start();

            _mini = new MiniHudWindow(_probes);
            _expanded = new ExpandedWindow(_probes);

            _tray = new NotifyIcon();
            _tray.Icon = System.Drawing.SystemIcons.Information;
            _tray.Visible = true;
            _tray.Text = "Netwatch";
            _tray.MouseClick += (s, ev) =>
            {
                if (ev.Button == MouseButtons.Left)
                {
                    ToggleMini();
                }
                else if (ev.Button == MouseButtons.Middle)
                {
                    _ = _probes!.RunUploadBloatTestAsync(TimeSpan.FromSeconds(10));
                }
            };

            var menu = new ContextMenuStrip();
            var showMini = new ToolStripMenuItem("Show/Hide Mini HUD");
            showMini.Click += (s, ev) => ToggleMini();
            var showExpanded = new ToolStripMenuItem("Show Expanded Panel");
            showExpanded.Click += (s, ev) => { _expanded!.Show(); _expanded.Activate(); };
            var copy = new ToolStripMenuItem("Copy Snapshot");
            copy.Click += (s, ev) => { try { System.Windows.Clipboard.SetText(_probes!.BuildSnapshot()); } catch { } };
            var runTest = new ToolStripMenuItem("Run 10s Upload Test");
            runTest.Click += async (s, ev) => await _probes!.RunUploadBloatTestAsync(TimeSpan.FromSeconds(10));
            var exit = new ToolStripMenuItem("Exit");
            exit.Click += (s, ev) => Shutdown();

            menu.Items.Add(showMini);
            menu.Items.Add(showExpanded);
            menu.Items.Add(copy);
            menu.Items.Add(runTest);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            _tray.ContextMenuStrip = menu;

            // Show mini HUD initially
            _mini.Show();
            _mini.Topmost = true;
        }

        public void ShowExpanded()
        {
            if (_expanded == null)
            {
                _expanded = new ExpandedWindow(_probes!);
            }
            _expanded.Show();
            _expanded.Activate();
        }

        private void ToggleMini()
        {
            if (_mini!.IsVisible) _mini.Hide();
            else { _mini.Show(); _mini.Activate(); }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tray!.Visible = false;
            _tray!.Dispose();
            _probes?.Dispose();
            base.OnExit(e);
        }
    }
}
