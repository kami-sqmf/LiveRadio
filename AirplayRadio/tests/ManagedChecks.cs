using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using AirplayRadio;
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            string dll = Path.GetFullPath(args[0]);
            var memoryLoaded = System.Reflection.Assembly.Load(File.ReadAllBytes(typeof(Program).Assembly.Location));
            if (!string.IsNullOrEmpty(memoryLoaded.Location)) throw new Exception("Expected memory-loaded assembly to have no location");
            string modAsset = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(dll)), "AirplayRadio.dll");
            if (ModPaths.NativeReceiver(modAsset) != dll) throw new Exception("Mod asset path did not resolve native receiver");
            try { ModPaths.NativeReceiver(""); throw new Exception("Empty mod asset path was accepted"); }
            catch (InvalidOperationException) { }
            Console.WriteLine("PASS: memory-loaded assembly regression; receiver resolves from mod asset path");
            var localized = L10n.Message("Gain: {0} dB", "增益：{0} dB", 6);
            var receiving = L10n.ReceiverStatus("Receiving audio");
            foreach (string locale in new[] { "zh-HANT", "zh-TW", "zh-HK" })
            {
                L10n.Locale = locale;
                if (localized.ToString() != "增益：6 dB" || receiving.ToString() != "正在接收音訊") throw new Exception("Traditional Chinese locale not applied");
            }
            foreach (string locale in new[] { "en-US", "ja-JP", "de-DE", null })
            {
                L10n.Locale = locale;
                if (localized.ToString() != "Gain: 6 dB" || receiving.ToString() != "Receiving audio") throw new Exception("English fallback or live language change failed");
            }
            LocalizedText literal = "Track {remix}";
            if (literal.ToString() != "Track {remix}" || L10n.ReceiverStatus("Unknown {detail}").ToString() != "Unknown {detail}") throw new Exception("Sender text treated as a format template");
            Console.WriteLine("PASS: live language switch, Traditional Chinese aliases, English fallback and literal metadata braces");
            RemoteChecks.Run();
            string key = Path.GetFullPath(args[1]);
            MediaChecks.Run(Path.Combine(Path.GetDirectoryName(key), "Artwork-checks"));
            Directory.CreateDirectory(Path.GetDirectoryName(key));
            for (int i = 0; i < 3; ++i)
            {
                var receiver = ReceiverSession.Start(dll, "Airplay Radio Managed Test", "02:41:52:00:00:02", key);
                if (!receiver.GetText(0).Contains("Waiting")) throw new Exception("Startup status missing");
                if (receiver.GetCover(receiver.CoverRevision)?.Length != 0) throw new Exception("Empty artwork snapshot missing");
                if (receiver.GetCover(ulong.MaxValue) != null) throw new Exception("Stale artwork snapshot accepted");
                var lifetime = (ReaderWriterLockSlim)typeof(ReceiverSession).GetField("_lifetime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(receiver);
                // Simulate metadata holding its lifetime read lock. Audio must run concurrently.
                lifetime.EnterReadLock();
                try {
                    Task.Run(() => { var data = new float[2048]; for (int n = 0; n < 1000; ++n) receiver.Read(data); }).Wait();
                    if (receiver.ReadMisses != 0) throw new Exception("Metadata reader excluded audio callbacks");
                } finally { lifetime.ExitReadLock(); }
                var reads = Task.Run(() => { var samples = new float[2048]; for (int n = 0; n < 20000; ++n) receiver.Read(samples); });
                receiver.Dispose(); receiver.Dispose(); reads.Wait();
                if (receiver.GetCover(0) != null) throw new Exception("Artwork accessible after disposal");
            }
            if (!File.Exists(key)) throw new Exception("UTF-8 identity key file missing");
            Console.WriteLine("PASS: .NET 4.8 native loading, UTF-8 key persistence, concurrent audio/dispose and idempotent cleanup");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
