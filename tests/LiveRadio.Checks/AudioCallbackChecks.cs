using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LiveRadio.Core;

internal static class AudioCallbackChecks
{
    // Reflection keeps this harness runnable against the saved 0.4.1 DLL, so
    // the exact same consumer/producer workload can compare old and new queues.
    private static PcmBuffer CreateBuffer()
    {
        var ctor = typeof(PcmBuffer).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(bool) });
        return ctor == null ? (PcmBuffer)Activator.CreateInstance(typeof(PcmBuffer), new object[] { 48000, 2, 2, 1 }) :
            (PcmBuffer)ctor.Invoke(new object[] { 48000, 2, 2, 1, true });
    }

    internal static async Task Run(Action<bool, string> assert)
    {
        await CheckPausedProducer(assert);
        var buffer = CreateBuffer();
        const int total = 12 * 1024 * 1024;
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            var producer = Task.Run(() =>
            {
                var block = new float[4096];
                for (int offset = 0; offset < total; offset += block.Length)
                {
                    for (int i = 0; i < block.Length; i++) block[i] = (offset + i) % 65536;
                    buffer.WriteBlocking(block, block.Length, deadline.Token);
                }
            });
            // The game can request a large PCM block. Mix small and one-second
            // reads rather than only exercising the earlier 20 ms test cadence.
            var consumer = Task.Run(() =>
            {
                var blocks = new[] { new float[256], new float[2048], new float[8192], new float[96000] };
                int offset = 0, gapsWithData = 0, iteration = 0;
                bool ordered = true;
                var timer = Stopwatch.StartNew();
                double longestReadMs = 0;
                while (offset < total)
                {
                    deadline.Token.ThrowIfCancellationRequested();
                    var block = blocks[iteration++ % blocks.Length];
                    if (block.Length > total - offset) block = new float[total - offset];
                    if (buffer.Available < (buffer.IsBuffering ? Math.Max(96000, block.Length) : block.Length))
                    { Thread.Yield(); continue; }
                    long start = timer.ElapsedTicks;
                    int count = buffer.Read(block);
                    longestReadMs = Math.Max(longestReadMs, (timer.ElapsedTicks - start) * 1000d / Stopwatch.Frequency);
                    if (count != block.Length) gapsWithData++;
                    for (int i = 0; i < count; i++) if (block[i] != (offset + i) % 65536) ordered = false;
                    offset += count;
                }
                Console.WriteLine($"Audio callback stress: samples={offset}; gapsWithQueuedData={gapsWithData}; lockMisses={buffer.Contentions}; maxReadMs={longestReadMs:F3}; ordered={ordered}.");
                return (gapsWithData, ordered);
            });
            try
            {
                await Task.WhenAll(producer, consumer);
                assert(consumer.Result.Item1 == 0 && buffer.Contentions == 0,
                    "Audio callbacks never insert silence when complete PCM blocks are already queued, under sustained producer contention");
                assert(consumer.Result.Item2 && buffer.DroppedSamples == 0 && buffer.Available == 0,
                    "Mixed-size audio callbacks preserve all 12 million stereo samples across ring wraparound");
            }
            finally { deadline.Cancel(); }
        }
    }

    private static async Task CheckPausedProducer(Action<bool, string> assert)
    {
        // Fault injection: a worker is descheduled while holding the old queue's
        // monitor. Queued audio must still be readable before that worker resumes.
        // This reproduces the audible failure without relying on CLR/Mono timing.
        var buffer = CreateBuffer();
        var input = new float[192000];
        for (int i = 0; i < input.Length; i++) input[i] = 0.25f;
        buffer.WriteBlocking(input, input.Length, CancellationToken.None);
        var gate = typeof(PcmBuffer).GetField("_gate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(buffer);
        using (var held = new ManualResetEventSlim())
        using (var release = new ManualResetEventSlim())
        {
            var worker = Task.Run(() => { lock (gate) { held.Set(); release.Wait(TimeSpan.FromSeconds(5)); } });
            if (!held.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Worker did not acquire the test monitor.");
            var output = new float[96000];
            var read = Task.Run(() => buffer.Read(output));
            bool returned = await Task.WhenAny(read, Task.Delay(250)) == read;
            release.Set(); await worker; await read;
            Console.WriteLine($"Paused producer: immediate={returned}; read={read.Result}; expected={output.Length}; lockMisses={buffer.Contentions}.");
            assert(returned && read.Result == output.Length && output[0] == 0.25f && output[output.Length - 1] == 0.25f,
                "A paused worker holding the producer monitor cannot silence or block queued audio");
        }
    }
}
