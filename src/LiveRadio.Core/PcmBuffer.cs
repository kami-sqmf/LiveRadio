using System;
using System.Threading;

namespace LiveRadio.Core
{
    public sealed class PcmBuffer
    {
        private readonly object _gate = new object();
        private readonly float[] _samples;
        private readonly int _threshold;
        private readonly bool _lossless;
        // Lossless queues have exactly one decoder writer and one audio reader.
        // Each owns its cursor; release/acquire publication protects the samples.
        private long _written, _consumed;
        private int _read, _count;
        private volatile bool _buffering = true;
        private long _droppedSamples, _underruns, _contentions;
        private long _readCalls, _readSamples, _silentSamples;
        public int SampleRate { get; }
        public int Channels { get; }
        public bool IsBuffering => _buffering;
        public int Available
        {
            get
            {
                if (_lossless)
                {
                    long read = Volatile.Read(ref _consumed);
                    return (int)Math.Max(0, Math.Min(_samples.Length, Volatile.Read(ref _written) - read));
                }
                lock (_gate) return _count;
            }
        }
        public long DroppedSamples => Interlocked.Read(ref _droppedSamples);
        public long Underruns => Interlocked.Read(ref _underruns);
        public long Contentions => Interlocked.Read(ref _contentions);
        public long ReadCalls => Interlocked.Read(ref _readCalls);
        public long ReadSamples => Interlocked.Read(ref _readSamples);
        public long SilentSamples => Interlocked.Read(ref _silentSamples);

        public PcmBuffer(int sampleRate, int channels, int seconds = 10, int prebufferSeconds = 2, bool lossless = false)
        {
            if (sampleRate < 8000 || sampleRate > 96000 || channels < 1 || channels > 2 ||
                seconds < 1 || seconds > 30 || prebufferSeconds < 1 || prebufferSeconds > seconds)
                throw new ArgumentOutOfRangeException();
            SampleRate = sampleRate;
            Channels = channels;
            _lossless = lossless;
            _samples = new float[sampleRate * channels * seconds];
            _threshold = sampleRate * channels * prebufferSeconds;
        }

        public void Write(float[] input, int count)
        {
            if (_lossless) throw new InvalidOperationException("Use WriteBlocking for a lossless PCM queue.");
            if (input == null || count < 0 || count > input.Length || count % Channels != 0)
                throw new ArgumentException("PCM input must contain whole audio frames.");
            lock (_gate)
            {
                int offset = Math.Max(0, count - _samples.Length);
                count -= offset;
                int drop = Math.Max(0, _count + count - _samples.Length);
                Interlocked.Add(ref _droppedSamples, offset + drop);
                _read = (_read + drop) % _samples.Length;
                _count -= drop;
                int write = (_read + _count) % _samples.Length;
                int first = Math.Min(count, _samples.Length - write);
                Array.Copy(input, offset, _samples, write, first);
                Array.Copy(input, offset + first, _samples, 0, count - first);
                _count += count;
            }
        }

        // Only the decoder worker waits for space. The audio callback takes no
        // monitor and signals no event: a descheduled worker cannot silence it.
        public void WriteBlocking(float[] input, int count, CancellationToken token)
        {
            if (!_lossless) throw new InvalidOperationException("WriteBlocking requires a lossless PCM queue.");
            if (input == null || count < 0 || count > input.Length || count % Channels != 0)
                throw new ArgumentException("PCM input must contain whole audio frames.");
            int offset = 0;
            while (offset < count)
            {
                token.ThrowIfCancellationRequested();
                long written = _written;
                int free = _samples.Length - (int)(written - Volatile.Read(ref _consumed));
                if (free == 0) { token.WaitHandle.WaitOne(5); continue; }
                int take = Math.Min(Math.Min(count - offset, free), 4096);
                take -= take % Channels;
                int write = (int)(written % _samples.Length);
                int first = Math.Min(take, _samples.Length - write);
                Array.Copy(input, offset, _samples, write, first);
                Array.Copy(input, offset + first, _samples, 0, take - first);
                Volatile.Write(ref _written, written + take);
                offset += take;
            }
        }

        // Unity calls this on its audio thread: never wait for networking or a producer lock.
        public int Read(float[] output)
        {
            int count = ReadCore(output);
            Interlocked.Increment(ref _readCalls);
            Interlocked.Add(ref _readSamples, count);
            Interlocked.Add(ref _silentSamples, output.Length - count);
            return count;
        }

        private int ReadCore(float[] output)
        {
            Array.Clear(output, 0, output.Length);
            if (_lossless) return ReadLossless(output);
            if (!Monitor.TryEnter(_gate)) { Interlocked.Increment(ref _contentions); return 0; }
            try
            {
                if (_buffering && _count < _threshold) return 0;
                _buffering = false;
                int count = Math.Min(output.Length - output.Length % Channels, _count);
                int first = Math.Min(count, _samples.Length - _read);
                Array.Copy(_samples, _read, output, 0, first);
                Array.Copy(_samples, 0, output, first, count - first);
                _read = (_read + count) % _samples.Length;
                _count -= count;
                if (count < output.Length) { _buffering = true; Interlocked.Increment(ref _underruns); }
                return count;
            }
            finally { Monitor.Exit(_gate); }
        }

        private int ReadLossless(float[] output)
        {
            long consumed = _consumed;
            int available = (int)(Volatile.Read(ref _written) - consumed);
            if (_buffering && available < _threshold) return 0;
            _buffering = false;
            int count = Math.Min(output.Length - output.Length % Channels, available);
            int read = (int)(consumed % _samples.Length);
            int first = Math.Min(count, _samples.Length - read);
            Array.Copy(_samples, read, output, 0, first);
            Array.Copy(_samples, 0, output, first, count - first);
            // Publish freed space only after copying; the writer cannot overwrite
            // unread samples, even when it resumes midway through this callback.
            Volatile.Write(ref _consumed, consumed + count);
            if (count < output.Length) { _buffering = true; Interlocked.Increment(ref _underruns); }
            return count;
        }
    }
}
