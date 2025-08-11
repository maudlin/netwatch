using System;

namespace Netwatch.Models
{
    // Welford's online algorithm for mean/variance
    public sealed class Welford
    {
        private long _n;
        private double _mean;
        private double _m2;

        public void Add(double x)
        {
            _n++;
            var delta = x - _mean;
            _mean += delta / _n;
            var delta2 = x - _mean;
            _m2 += delta * delta2;
        }

        public void Reset()
        {
            _n = 0; _mean = 0; _m2 = 0;
        }

        public long Count => _n;
        public double Mean => _mean;
        public double Variance => _n > 1 ? _m2 / _n : 0.0;
        public double StdDev => Math.Sqrt(Variance);
    }

    // P^2 quantile estimator (single quantile)
    // Based on Jain & Chlamtac, 1985. Simplified implementation for one quantile.
    public sealed class P2QuantileEstimator
    {
        private readonly double _p; // desired quantile (0..1)
        private int _count;
        private readonly double[] _q = new double[5];
        private readonly double[] _n = new double[5];
        private readonly double[] _np = new double[5];
        private readonly double[] _dn = new double[5];

        public P2QuantileEstimator(double p)
        {
            if (p <= 0 || p >= 1) throw new ArgumentOutOfRangeException(nameof(p));
            _p = p;
        }

        public void Reset()
        {
            _count = 0;
        }

        public void Add(double x)
        {
            if (_count < 5)
            {
                _q[_count] = x;
                _count++;
                if (_count == 5)
                {
                    Array.Sort(_q);
                    _n[0] = 1; _n[1] = 2; _n[2] = 3; _n[3] = 4; _n[4] = 5;
                    _np[0] = 1;
                    _np[1] = 1 + 2 * _p;
                    _np[2] = 1 + 4 * _p;
                    _np[3] = 3 + 2 * _p;
                    _np[4] = 5;
                    _dn[0] = 0;
                    _dn[1] = _p / 2;
                    _dn[2] = _p;
                    _dn[3] = (1 + _p) / 2;
                    _dn[4] = 1;
                }
                return;
            }

            // Update positions and heights
            int k;
            if (x < _q[0]) { _q[0] = x; k = 0; }
            else if (x < _q[1]) { k = 0; }
            else if (x < _q[2]) { k = 1; }
            else if (x < _q[3]) { k = 2; }
            else if (x <= _q[4]) { k = 3; }
            else { _q[4] = x; k = 3; }

            for (int i = k + 1; i < 5; i++) _n[i]++;
            _np[1] += _dn[1];
            _np[2] += _dn[2];
            _np[3] += _dn[3];

            // Adjust heights if necessary
            for (int i = 1; i <= 3; i++)
            {
                var d = _np[i] - _n[i];
                if ((d >= 1 && _n[i + 1] - _n[i] > 1) || (d <= -1 && _n[i - 1] - _n[i] < -1))
                {
                    var dsign = Math.Sign(d);
                    var qip = Parabolic(i, dsign);
                    if (_q[i - 1] < qip && qip < _q[i + 1])
                    {
                        _q[i] = qip;
                    }
                    else
                    {
                        _q[i] = Linear(i, dsign);
                    }
                    _n[i] += dsign;
                }
            }
        }

        private double Parabolic(int i, int d)
        {
            return _q[i] + d / (_n[i + 1] - _n[i - 1]) * ((
                (_n[i] - _n[i - 1] + d) * (_q[i + 1] - _q[i]) / (_n[i + 1] - _n[i])
                ) + ((
                _n[i + 1] - _n[i] - d) * (_q[i] - _q[i - 1]) / (_n[i] - _n[i - 1])
                ))
            ;
        }

        private double Linear(int i, int d)
        {
            return _q[i] + d * (_q[i + d] - _q[i]) / (_n[i + d] - _n[i]);
        }

        public double Current
        {
            get
            {
                if (_count == 0) return double.NaN;
                if (_count < 5)
                {
                    var tmp = new double[_count];
                    Array.Copy(_q, tmp, _count);
                    Array.Sort(tmp);
                    var rank = (int)Math.Round((_count - 1) * _p);
                    return tmp[Math.Clamp(rank, 0, _count - 1)];
                }
                return _q[2];
            }
        }
    }

    // Ring counter over a fixed window for success/failure, allows O(1) loss% updates
    public sealed class RingCounter
    {
        private readonly byte[] _window;
        private int _head;
        private int _count;
        private int _sum; // number of successes in window

        public RingCounter(int windowSize)
        {
            if (windowSize <= 0) throw new ArgumentOutOfRangeException(nameof(windowSize));
            _window = new byte[windowSize];
            _head = 0; _count = 0; _sum = 0;
        }

        public void Add(bool success)
        {
            byte val = (byte)(success ? 1 : 0);
            if (_count < _window.Length)
            {
                _window[_head] = val;
                _sum += val;
                _head = (_head + 1) % _window.Length;
                _count++;
            }
            else
            {
                // overwrite
                _head = (_head + 1) % _window.Length;
                int idx = (_head - 1 + _window.Length) % _window.Length;
                _sum -= _window[idx];
                _window[idx] = val;
                _sum += val;
            }
        }

        public void Reset()
        {
            Array.Fill(_window, (byte)0); _head = 0; _count = 0; _sum = 0;
        }

        public int WindowSize => _window.Length;
        public int Count => _count;
        public int Successes => _sum;
        public int Failures => _count - _sum;
        public double LossPercent => _count == 0 ? 0.0 : 100.0 * (_count - _sum) / _count;
    }
}

