using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LiveRadio.Core;

internal static class ReleaseChecks
{
    internal static async Task Run(Action<bool, string> assert)
    {
        var message = L10n.Message("Saved: {0}", "已收藏：{0}", "中廣音樂網");
        L10n.Locale = "en-US"; assert(message.ToString() == "Saved: 中廣音樂網", "English default leaves station names unchanged");
        L10n.Locale = "zh-HANT"; assert(message.ToString() == "已收藏：中廣音樂網", "Existing messages follow a Traditional Chinese locale change");
        L10n.Locale = "fr-FR"; assert(message.ToString() == "Saved: 中廣音樂網", "Other languages fall back to English");
        L10n.Locale = "en-US";
        string directory = Path.Combine(Path.GetTempPath(), "LiveRadio-release-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "stations.json");
            foreach (string name in new[] { "中廣音樂網", "Antenne" })
            {
                string icon = Path.Combine(directory, name + ".png");
                LiveRadio.StationArtwork.CreateInitial(name, icon);
                using (var png = new System.Drawing.Bitmap(icon))
                    assert(png.Width == 96 && png.Height == 96 && png.GetPixel(0, 0).A == 0, "Native initial artwork is a transparent PNG for " + name);
            }
            foreach (string svg in new[] { "<svg xmlns='http://www.w3.org/2000/svg'><script>bad()</script></svg>", "<svg xmlns='http://www.w3.org/2000/svg'><image href='file:///secret'/></svg>", "<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///secret'>]><svg>&x;</svg>" })
            {
                bool blocked = false;
                try { using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg))) LiveRadio.StationArtwork.ReadSafeSvg(stream); } catch { blocked = true; }
                assert(blocked, "Downloaded SVG cannot execute scripts or load external files");
            }
            var library = new StationLibrary(file, new CatalogQuery());
            var station = library.AddCustom("  Custom station  ", "https://example.com/live.m3u8?token=123", "AUTO");
            assert(station.Name == "Custom station" && station.Custom && station.Format == StreamFormat.Hls && station.StreamUri.Query == "?token=123", "Manual HLS recognizes the extension and preserves signed URL queries");
            var reloaded = new StationLibrary(file, new CatalogQuery());
            assert(reloaded.State.Favorites.Single().Id == station.Id && reloaded.State.Favorites.Single().Custom && reloaded.State.Recent.Count == 0, "Custom identity and HLS favorite survive restart without playing");
            bool rejected = false;
            try { library.AddCustom("Duplicate", station.Url, "HLS"); } catch (ArgumentException) { rejected = true; }
            assert(rejected && library.State.Favorites.Count == 1, "Duplicate manual URL never toggles off the existing favorite");
            foreach (string url in new[] { "file:///C:/audio.mp3", "ftp://example.com/live", "https://user:secret@example.com/live", "https://example.com/live#part", "", "not a URL" })
            {
                rejected = false; try { library.AddCustom("Station", url, "AUTO"); } catch (ArgumentException) { rejected = true; }
                assert(rejected, "Manual entry rejects invalid URL: " + url);
            }
            assert(RadioStation.CreateCustom("MP3", "https://example.com/stream", "MP3").Format == StreamFormat.Mp3, "An extensionless direct MP3 can use the built-in decoder");
            assert(RadioStation.CreateCustom("Auto", "https://example.com/stream", "AUTO").Format == StreamFormat.Auto, "An unknown custom URL uses the bounded FFmpeg format allowlist");
            assert(new RadioStation { Id = station.Id, Name = "API", Codec = "AUTO", Url = station.Url.Replace(".m3u8", "") }.Format == StreamFormat.Unsupported, "Auto probing is restricted to user-added stations");
            library.Toggle(station); library.Undo();
            assert(library.State.Favorites.Single().Id == station.Id, "Remove and undo keep a custom station's identity");
            for (int i = 1; i < 50; i++) library.AddCustom("Station " + i, "https://example.com/" + i, "MP3");
            rejected = false; try { library.AddCustom("Too many", "https://example.com/overflow", "MP3"); } catch (ArgumentException) { rejected = true; }
            assert(rejected && library.State.Favorites.Count == 50, "Manual additions respect the same 50-favorite limit");
            assert(FfmpegDecoder.FindExecutable("bad\0path") == null && FfmpegDecoder.FindExecutable("relative.exe") == null, "Malformed decoder settings do not break the mod");
            foreach (var format in new[] { StreamFormat.Aac, StreamFormat.Ogg, StreamFormat.Hls, StreamFormat.Auto })
            {
                using (var session = new LiveStreamSession(new Uri("https://example.com/live"), format, Path.Combine(directory, "missing.exe")))
                {
                    assert(await Task.WhenAny(session.Completion, Task.Delay(3000)) == session.Completion && session.State == StreamState.Failed && session.Error.Contains("FFmpeg"), "Missing FFmpeg fails promptly with instructions for " + format);
                }
            }
            string args = FfmpegDecoder.BuildArguments(new Uri("https://example.com/live.m3u8?value=a%22b"), StreamFormat.Hls);
            assert(args.Contains("-f hls") && args.Contains("-map 0:a:0") && !args.Contains("whitelist ALL") && !args.Contains("tcp,tls,crypto,file"), "HLS selects audio with restricted nested protocols");
        }
        finally { Directory.Delete(directory, true); }
    }
}
