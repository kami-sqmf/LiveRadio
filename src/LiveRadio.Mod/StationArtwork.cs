using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LiveRadio.Core;

namespace LiveRadio
{
    // Local artwork only is exposed to Gameface. Fetches start when the radio panel is opened.
    internal sealed class StationArtwork : IDisposable
    {
        private sealed class Pending { public RadioStation Station; public Task<(string file, string favicon)> Task; }
        private readonly string _directory;
        private readonly RadioBrowserClient _browser;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly SemaphoreSlim _slots = new SemaphoreSlim(2);
        private readonly Dictionary<string, string> _icons = new Dictionary<string, string>();
        private readonly HashSet<string> _attempted = new HashSet<string>();
        private readonly List<Pending> _pending = new List<Pending>();
        private bool _viewed;
        public StationArtwork(string directory, RadioBrowserClient browser) { _directory = directory; _browser = browser; Directory.CreateDirectory(directory); }
        public void Open(IEnumerable<RadioStation> stations) { _viewed = true; foreach (var station in stations) Get(station); }
        public string Get(RadioStation station, bool createInitial = true)
        {
            string id = Guid.Parse(station.Id).ToString("N");
            if (!_icons.TryGetValue(id, out string file))
            {
                file = "";
                foreach (string extension in new[] { ".png", ".svg" })
                    if (File.Exists(Path.Combine(_directory, id + extension))) { file = id + extension; break; }
                _icons[id] = file;
            }
            // Explorer rows render their own initial while the bounded download queue runs.
            // Native stations still need a local image for the game's station selector.
            if (file.Length == 0 && createInitial)
            {
                file = id + "-initial.png";
                if (!File.Exists(Path.Combine(_directory, file)))
                    try { CreateInitial(station.Name, Path.Combine(_directory, file)); }
                    catch
                    {
                        file = id + "-initial.svg";
                        File.WriteAllText(Path.Combine(_directory, file), "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"96\" height=\"96\"><circle cx=\"48\" cy=\"48\" r=\"47\" fill=\"#254958\"/><text x=\"48\" y=\"63\" text-anchor=\"middle\" font-size=\"42\" fill=\"#eaf2f5\">" + System.Security.SecurityElement.Escape(StringInfo.GetNextTextElement(station.Name.Trim())) + "</text></svg>");
                    }
                _icons[id] = file;
            }
            if (_viewed && !station.Custom && (file.Length == 0 || file.Contains("-initial.")) &&
                (createInitial || !string.IsNullOrWhiteSpace(station.Favicon)) && _attempted.Add(id))
            {
                var token = _cancel.Token;
                _pending.Add(new Pending { Station = station, Task = Task.Run(async () =>
                {
                    await _slots.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            deadline.CancelAfter(TimeSpan.FromSeconds(15));
                            string favicon = string.IsNullOrWhiteSpace(station.Favicon) ? _browser.GetFavicon(station.Id, deadline.Token) : station.Favicon;
                            return (Download(favicon, Path.Combine(_directory, id), deadline.Token), favicon);
                        }
                    }
                    finally { _slots.Release(); }
                }, token) });
            }
            return file.Length == 0 ? "" : "coui://liveradio-icons/" + file;
        }
        public bool Tick()
        {
            bool changed = false;
            foreach (var pending in _pending.Where(p => p.Task.IsCompleted).ToArray())
            {
                _pending.Remove(pending);
                if (pending.Task.Status != TaskStatus.RanToCompletion) { var observed = pending.Task.Exception; continue; }
                var result = pending.Task.Result;
                pending.Station.Favicon = result.favicon;
                if (result.file != null) _icons[Guid.Parse(pending.Station.Id).ToString("N")] = Path.GetFileName(result.file);
                changed = true;
            }
            return changed;
        }
        internal static void CreateInitial(string name, string path)
        {
            using (var image = new Bitmap(96, 96))
            using (var graphics = Graphics.FromImage(image))
            using (var background = new SolidBrush(Color.FromArgb(37, 73, 88)))
            using (var foreground = new SolidBrush(Color.FromArgb(234, 242, 245)))
            using (var font = new Font("Microsoft JhengHei UI", 42, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.Clear(Color.Transparent); graphics.FillEllipse(background, 1, 1, 94, 94);
                graphics.DrawString(StringInfo.GetNextTextElement(name.Trim()), font, foreground, new RectangleF(0, 0, 96, 96), alignment);
                image.Save(path, ImageFormat.Png);
            }
        }
        internal static string Download(string source, string path, CancellationToken token)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo)) return null;
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = RadioBrowserClient.UserAgent; request.Timeout = 8000; request.ReadWriteTimeout = 8000; request.MaximumAutomaticRedirections = 3;
            using (token.Register(request.Abort))
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var content = new MemoryStream())
            {
                var buffer = new byte[8192]; int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (content.Length + read > 512 * 1024) throw new IOException("Artwork exceeds 512 KiB.");
                    content.Write(buffer, 0, read);
                }
                content.Position = 0;
                if ((response.ContentType ?? "").IndexOf("svg", StringComparison.OrdinalIgnoreCase) >= 0 || uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    string svg = ReadSafeSvg(content);
                    File.WriteAllText(path + ".svg", svg); return path + ".svg";
                }
                using (var original = Image.FromStream(content, false, true))
                {
                    if (original.Width > 4096 || original.Height > 4096) throw new IOException("Artwork dimensions exceed 4096 px.");
                    using (var image = new Bitmap(96, 96))
                    using (var graphics = Graphics.FromImage(image))
                    {
                        graphics.Clear(Color.Transparent); graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        float scale = Math.Min(96f / original.Width, 96f / original.Height);
                        float width = original.Width * scale, height = original.Height * scale;
                        graphics.DrawImage(original, (96 - width) / 2, (96 - height) / 2, width, height);
                        image.Save(path + ".png", ImageFormat.Png); return path + ".png";
                    }
                }
            }
        }
        internal static string ReadSafeSvg(Stream input)
        {
            var xml = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 512 * 1024 })) xml.Load(reader);
            if (xml.DocumentElement?.LocalName != "svg") throw new IOException("Invalid SVG.");
            var allowed = new HashSet<string>(new[] { "svg", "g", "path", "circle", "ellipse", "rect", "line", "polyline", "polygon", "defs", "linearGradient", "radialGradient", "stop", "clipPath", "mask", "title", "desc", "text", "tspan", "use" });
            foreach (XmlElement element in xml.SelectNodes("//*"))
            {
                if (!allowed.Contains(element.LocalName)) throw new IOException("Unsupported SVG element.");
                foreach (XmlAttribute attr in element.Attributes)
                {
                    string value = attr.Value.Trim();
                    if (attr.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        attr.LocalName == "href" && !value.StartsWith("#") || value.IndexOf("url(", StringComparison.OrdinalIgnoreCase) >= 0 && !System.Text.RegularExpressions.Regex.IsMatch(value, @"^url\(#[a-zA-Z0-9_-]+\)$"))
                        throw new IOException("External or active SVG content is not supported.");
                }
            }
            return xml.DocumentElement.OuterXml;
        }
        public void Dispose()
        {
            _cancel.Cancel();
            Task.WhenAll(_pending.Select(p => p.Task)).ContinueWith(t => { var observed = t.Exception; _cancel.Dispose(); _slots.Dispose(); });
        }
    }
}
