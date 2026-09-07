using System;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using NLayer;

namespace LiveRadio.Core
{
    internal static class Mp3Decoder
    {
        internal static void Receive(Stream stream, CancellationToken token, Action<PcmBuffer> publish)
        {
            using (var frames = new FrameReader(stream))
            {
                var decoder = new MpegFrameDecoder();
                var samples = new float[2304];
                PcmBuffer output = null;
                IMpegFrame frame;
                while ((frame = frames.Next()) != null)
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        int channels = frame.ChannelMode == MpegChannelMode.Mono ? 1 : 2;
                        if (output == null || output.SampleRate != frame.SampleRate || output.Channels != channels)
                        {
                            // Broadcasters can splice an intro with a different format into
                            // the same connection. Never put that PCM into the old Unity clip.
                            decoder.Reset();
                            output = new PcmBuffer(frame.SampleRate, channels, lossless: true);
                            publish(output);
                        }
                        int read;
                        try { read = decoder.DecodeFrame(frame, samples, 0); }
                        catch (InvalidDataException) { decoder.Reset(); continue; }
                        output.WriteBlocking(samples, read, token);
                    }
                    finally { frames.Release(frame); }
                }
            }
        }

        // NLayer 1.16 exposes the frame decoder publicly, but its frame reader is internal.
        // Isolate the pinned dependency access here so format changes are detected at exact
        // frame boundaries instead of guessing from a mixed MpegFile.ReadSamples block.
        private sealed class FrameReader : IDisposable
        {
            private const BindingFlags Members = BindingFlags.Instance | BindingFlags.NonPublic;
            private readonly MpegFile _file;
            private readonly object _reader;
            private readonly MethodInfo _next, _release;
            public FrameReader(Stream stream)
            {
                var readerField = typeof(MpegFile).GetField("_reader", Members);
                _next = readerField?.FieldType.GetMethod("NextFrame", Members);
                _release = typeof(MpegFile).Assembly.GetType("NLayer.Decoder.FrameBase")?.GetMethod("ClearBuffer", Members);
                if (readerField == null || _next == null || _release == null)
                    throw new NotSupportedException("The bundled NLayer MP3 decoder is incompatible. Reinstall Live Radio with its included NLayer.dll.");
                _file = new MpegFile(stream);
                _reader = readerField.GetValue(_file);
            }
            public IMpegFrame Next() => (IMpegFrame)Invoke(_next, _reader);
            public void Release(IMpegFrame frame) => Invoke(_release, frame);
            public void Dispose() => _file.Dispose();
            private static object Invoke(MethodInfo method, object target)
            {
                try { return method.Invoke(target, null); }
                catch (TargetInvocationException ex) when (ex.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw;
                }
            }
        }
    }
}
