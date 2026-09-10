using System;
using System.Threading;

namespace AirplayRadio
{
    // One audio consumer; the UI only publishes the target. No allocation or locks in Process.
    internal sealed class LocalGain
    {
        private float _target = 1;
        private double _gain = 1, _limiter = 1;
        private long _limitedFrames;
        internal long LimitedFrames => Interlocked.Read(ref _limitedFrames);
        internal void SetDecibels(int db) => Volatile.Write(ref _target, (float)Math.Pow(10, Math.Max(0, Math.Min(12, db)) / 20.0));
        internal void Process(float[] samples)
        {
            float target = Volatile.Read(ref _target);
            if (target == 1 && _gain == 1 && _limiter == 1) return;
            const double gainStep = 0.0022650047; // 10ms smoothing, 44.1kHz
            const double release = 0.0002267317; // 100ms limiter release
            long limited = 0;
            for (int i = 0; i + 1 < samples.Length; i += 2)
            {
                _gain += (target - _gain) * gainStep;
                if (Math.Abs(target - _gain) < target * 0.00001) _gain = target;
                double left = samples[i] * _gain, right = samples[i + 1] * _gain;
                double peak = Math.Max(Math.Abs(left), Math.Abs(right));
                double required = peak > 1 ? 1 / peak : 1;
                _limiter = required < _limiter ? required : _limiter + (required - _limiter) * release;
                if (_limiter > 0.99999) _limiter = 1;
                if (_limiter < 0.99999) ++limited;
                samples[i] = (float)Math.Max(-1, Math.Min(1, left * _limiter));
                samples[i + 1] = (float)Math.Max(-1, Math.Min(1, right * _limiter));
            }
            if (limited != 0) Interlocked.Add(ref _limitedFrames, limited);
        }
    }
}
