using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiveRadio.Core;

internal static class BufferContinuityChecks
{
    internal static async Task Run(Action<bool, string> assert)
    {
        var ring = new PcmBuffer(8000, 2, 1, 1, lossless: true);
        var input = Enumerable.Range(0, 64000).Select(n => (float)n).ToArray();
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            var writer = Task.Run(() => ring.WriteBlocking(input, input.Length, timeout.Token));
            var actual = new float[input.Length]; var block = new float[256]; int position = 0;
            while (position < actual.Length)
            {
                timeout.Token.ThrowIfCancellationRequested();
                int read = ring.Read(block);
                Array.Copy(block, 0, actual, position, read); position += read;
                await Task.Delay(1);
            }
            await writer;
            assert(actual.SequenceEqual(input) && ring.DroppedSamples == 0,
                "Segment-sized bursts exceeding capacity retain every stereo sample in order");
        }
        var full = new PcmBuffer(8000, 2, 1, 1, lossless: true);
        full.WriteBlocking(input, 16000, CancellationToken.None);
        using (var cancel = new CancellationTokenSource())
        {
            var writer = Task.Run(() => full.WriteBlocking(input, 2, cancel.Token));
            assert(await Task.WhenAny(writer, Task.Delay(80)) != writer,
                "A full PCM queue backpressures the decoder instead of overwriting unplayed audio");
            cancel.Cancel();
            assert(await Task.WhenAny(writer, Task.Delay(500)) == writer,
                "Cancel interrupts a producer waiting for PCM space within 500 ms");
            try { await writer; } catch (OperationCanceledException) { }
            var retained = new float[16000]; full.Read(retained);
            assert(retained.SequenceEqual(input.Take(16000)) && full.DroppedSamples == 0,
                "Canceling a blocked writer preserves the queued audio");
        }
    }
}
