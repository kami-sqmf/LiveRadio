using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LiveRadio.Core;

internal static class ContinuityChecks
{
    // Consume at audio-clock speed across several real segment boundaries, not just the first PCM chunk.
    internal static async Task Run(Uri uri, StreamFormat format, int seconds, bool strict, Action<bool, string> assert)
    {
        using (var session = new LiveStreamSession(uri, format))
        {
            var clock = Stopwatch.StartNew();
            PcmBuffer output = null;
            float[] block = null;
            double nextRead = 0, nextReport = 0, firstAudio = -1;
            int replacements = 0, silentBlocks = 0;
            long readSamples = 0;
            double minBuffer = double.MaxValue, maxBuffer = 0;
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                var current = session.Output;
                if (current != output)
                {
                    if (output != null) replacements++;
                    output = current;
                    if (output != null) block = new float[output.SampleRate / 50 * output.Channels];
                }
                double now = clock.Elapsed.TotalMilliseconds;
                if (output != null && now >= nextRead)
                {
                    nextRead = Math.Max(nextRead + 20, now - 200);
                    int count = output.Read(block);
                    readSamples += count;
                    if (count > 0 && firstAudio < 0) firstAudio = clock.Elapsed.TotalSeconds;
                    if (firstAudio >= 0)
                    {
                        if (count < block.Length) silentBlocks++;
                        double buffered = output.Available / (double)(output.SampleRate * output.Channels);
                        minBuffer = Math.Min(minBuffer, buffered); maxBuffer = Math.Max(maxBuffer, buffered);
                    }
                }
                else await Task.Delay(1);
                if (clock.Elapsed.TotalSeconds >= nextReport)
                {
                    nextReport += 10;
                    if (output != null)
                        Console.WriteLine($"t={clock.Elapsed.TotalSeconds:F1}s buffer={output.Available / (double)(output.SampleRate * output.Channels):F2}s dropped={output.DroppedSamples} underruns={output.Underruns} lockMisses={output.Contentions} state={session.State}");
                    else Console.WriteLine($"t={clock.Elapsed.TotalSeconds:F1}s state={session.State}; error={session.Error}");
                }
                if (session.State == StreamState.Failed) throw new Exception("Continuity stream failed: " + session.Error);
            }
            Console.WriteLine($"RESULT firstAudio={firstAudio:F2}s bufferRange={minBuffer:F2}..{maxBuffer:F2}s consumed={readSamples} incompleteBlocks={silentBlocks} replacements={replacements} dropped={output?.DroppedSamples} underruns={output?.Underruns} lockMisses={output?.Contentions}");
            assert(firstAudio >= 0 && readSamples > 0, "Continuity run received audio");
            if (strict) assert(output != null && output.DroppedSamples == 0 && output.Underruns == 0 && output.Contentions == 0 && silentBlocks == 0 && replacements == 0,
                "Continuous playback has no dropped PCM, starvation, contention gaps or decoder replacements");
            session.Dispose();
            assert(await Task.WhenAny(session.Completion, Task.Delay(5000)) == session.Completion, "Continuity decoder stops within five seconds");
        }
    }
}
