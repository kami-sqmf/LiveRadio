using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AirplayRadio
{
    // Runs on a worker, never on the RTP, Unity audio or UI thread.
    internal static class MediaArtwork
    {
        internal const int Size = 192;
        private const int MaxBytes = 2 * 1024 * 1024;
        private static readonly Regex CacheName = new Regex(@"^cover-[0-9a-f]{64}\.png$", RegexOptions.CultureInvariant);

        internal static string Save(byte[] bytes, string directory)
        {
            if (bytes == null || bytes.Length == 0) return null;
            Validate(bytes);
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "cover-" + hash + ".png");
            if (!File.Exists(path))
            {
                using (var input = new MemoryStream(bytes, false))
                using (var original = Image.FromStream(input, false, true))
                {
                    CheckDimensions(original.Width, original.Height);
                    const int large = Size * 4;
                    using (var square = new Bitmap(large, large, PixelFormat.Format32bppPArgb))
                    using (var circle = new Bitmap(large, large, PixelFormat.Format32bppPArgb))
                    using (var small = new Bitmap(Size, Size, PixelFormat.Format32bppPArgb))
                    {
                        using (var g = Graphics.FromImage(square))
                        using (var edge = new ImageAttributes())
                        {
                            Quality(g);
                            edge.SetWrapMode(WrapMode.TileFlipXY);
                            int crop = Math.Min(original.Width, original.Height);
                            g.DrawImage(original, new Rectangle(0, 0, large, large), (original.Width - crop) / 2f,
                                (original.Height - crop) / 2f, crop, crop, GraphicsUnit.Pixel, edge);
                        }
                        using (var g = Graphics.FromImage(circle))
                        using (var texture = new TextureBrush(square))
                        {
                            Quality(g);
                            g.FillEllipse(texture, 2, 2, large - 4, large - 4);
                        }
                        using (var g = Graphics.FromImage(small))
                        {
                            Quality(g);
                            g.DrawImage(circle, new Rectangle(0, 0, Size, Size), 0, 0, large, large, GraphicsUnit.Pixel);
                        }
                        // COUI learns the URL only after the complete PNG has been written.
                        small.Save(path, ImageFormat.Png);
                    }
                }
            }
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            Prune(directory, path);
            return path;
        }

        private static void Quality(Graphics g)
        {
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        private static void Prune(string directory, string current)
        {
            // Only our own hash-named thumbnails; no sender-supplied filenames or paths.
            foreach (var file in new DirectoryInfo(directory).GetFiles("cover-*.png")
                .Where(f => CacheName.IsMatch(f.Name) && f.FullName != current)
                .OrderByDescending(f => f.LastWriteTimeUtc).Skip(7))
            {
                try { file.Delete(); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void CheckDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || (long)width * height > 16777216)
                throw new InvalidDataException("Artwork dimensions exceed the supported limit");
        }

        // Reject oversized dimensions before giving compressed data to the image decoder.
        private static void Validate(byte[] b)
        {
            if (b.Length > MaxBytes) throw new InvalidDataException("Artwork exceeds 2 MiB");
            if (b.Length >= 33 && b[0] == 137 && b[1] == 80 && b[2] == 78 && b[3] == 71 &&
                b[4] == 13 && b[5] == 10 && b[6] == 26 && b[7] == 10 &&
                b[8] == 0 && b[9] == 0 && b[10] == 0 && b[11] == 13 &&
                b[12] == 73 && b[13] == 72 && b[14] == 68 && b[15] == 82)
            {
                CheckDimensions(b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19],
                    b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23]);
                return;
            }
            if (b.Length >= 4 && b[0] == 0xff && b[1] == 0xd8)
            {
                int p = 2;
                while (p < b.Length)
                {
                    if (b[p++] != 0xff) break;
                    while (p < b.Length && b[p] == 0xff) ++p;
                    if (p >= b.Length) break;
                    int marker = b[p++];
                    if (marker == 0xd9 || marker == 0xda) break;
                    if (marker == 0x01 || (marker >= 0xd0 && marker <= 0xd8)) continue;
                    if (p + 2 > b.Length) break;
                    int length = b[p] << 8 | b[p + 1];
                    if (length < 2 || length > b.Length - p) break;
                    bool frame = marker >= 0xc0 && marker <= 0xcf && marker != 0xc4 && marker != 0xc8 && marker != 0xcc;
                    if (frame && length >= 8)
                    {
                        CheckDimensions(b[p + 5] << 8 | b[p + 6], b[p + 3] << 8 | b[p + 4]);
                        return;
                    }
                    p += length;
                }
            }
            throw new InvalidDataException("Artwork is not a supported JPEG or PNG");
        }
    }
}
