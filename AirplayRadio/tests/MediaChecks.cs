using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AirplayRadio;

internal static class MediaChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static float[] Tone(float left, float right, int frames = 44100)
    {
        var samples = new float[frames * 2];
        for (int i = 0; i < samples.Length; i += 2) { samples[i] = left; samples[i + 1] = right; }
        return samples;
    }
    private static byte[] Picture(ImageFormat format, int variation = 0)
    {
        using (var picture = new Bitmap(320, 200))
        using (var g = Graphics.FromImage(picture))
        using (var bytes = new MemoryStream())
        {
            g.Clear(Color.FromArgb(20 + variation, 180, 160));
            g.FillRectangle(Brushes.DarkBlue, 0, 0, 50, 200);
            g.FillRectangle(Brushes.DarkRed, 270, 0, 50, 200);
            picture.Save(bytes, format); return bytes.ToArray();
        }
    }
    internal static void Run(string directory)
    {
        var gain = new LocalGain();
        var samples = Tone(.1f, -.05f);
        var original = (float[])samples.Clone(); gain.Process(samples);
        Check(samples.SequenceEqual(original), "0 dB must preserve samples exactly");
        gain.SetDecibels(6); gain.Process(samples);
        Check(samples[0] > .1f && samples[0] < .101f, "Gain changes ramp smoothly");
        Check(Math.Abs(samples[samples.Length - 2] - .199526f) < .0001, "+6 dB amplitude");
        gain.SetDecibels(100); samples = Tone(.1f, -.05f); gain.Process(samples);
        Check(Math.Abs(samples[samples.Length - 2] - .398107f) < .0001, "Gain capped at +12 dB");
        samples = Tone(.9f, -.45f); gain.Process(samples);
        Check(samples.All(x => x >= -1 && x <= 1), "Limited output cannot exceed full scale");
        Check(Math.Abs(samples[samples.Length - 2] - 1) < .0001 && Math.Abs(samples[samples.Length - 1] + .5) < .0001,
            "Stereo-linked limiter preserves channel balance");
        Check(gain.LimitedFrames > 0, "Limiter activity recorded");
        samples = new float[88200]; gain.Process(samples);
        Check(samples.All(x => x == 0), "Gain never creates sound from mute/silence");
        gain.SetDecibels(-100); gain.Process(Tone(.1f, -.05f));
        samples = Tone(.1f, -.05f); original = (float[])samples.Clone(); gain.Process(samples);
        Check(samples.SequenceEqual(original), "Returning to 0 dB settles back to exact bypass");
        Console.WriteLine("PASS: local dB gain, smooth changes, full-scale limiter, stereo balance, silence and zero-gain bypass");

        Directory.CreateDirectory(directory);
        foreach (var format in new[] { ImageFormat.Png, ImageFormat.Jpeg })
        {
            var input = Picture(format);
            string path = MediaArtwork.Save(input, directory);
            Check(path == MediaArtwork.Save(input, directory), "Repeated artwork shares a content-addressed URL");
            using (var thumbnail = new Bitmap(path))
            {
                Check(thumbnail.Width == MediaArtwork.Size && thumbnail.Height == MediaArtwork.Size, "Thumbnail dimensions");
                Check(thumbnail.GetPixel(0, 0).A == 0 && thumbnail.GetPixel(thumbnail.Width - 1, thumbnail.Height - 1).A == 0,
                    "Circular artwork has transparent corners");
                var center = thumbnail.GetPixel(thumbnail.Width / 2, thumbnail.Height / 2);
                Check(center.A == 255 && center.G > 150 && center.R < 40, "Artwork center retains decoded source color");
                bool antialiased = false;
                for (int x = 0; x < thumbnail.Width; ++x)
                    for (int y = 0; y < 8; ++y)
                    { int a = thumbnail.GetPixel(x, y).A; if (a > 0 && a < 255) antialiased = true; }
                Check(antialiased, "Circular edge includes antialiased alpha");
            }
        }
        void Reject(byte[] bytes)
        {
            try { MediaArtwork.Save(bytes, directory); throw new Exception("Invalid artwork was accepted"); }
            catch (InvalidDataException) { }
        }
        Reject(new byte[] { 1, 2, 3, 4 });
        Reject(new byte[2 * 1024 * 1024 + 1]);
        var oversized = Picture(ImageFormat.Png); oversized[16] = 1; Reject(oversized);
        Reject(new byte[] { 255,216,255,224,255,255 });
        Check(MediaArtwork.Save(Array.Empty<byte>(), directory) == null && MediaArtwork.Save(null, directory) == null,
            "Missing/cleared artwork falls back to station icon");
        string unrelated = Path.Combine(directory, "cover-user-file.png"); File.WriteAllText(unrelated, "keep");
        for (int i = 1; i < 12; ++i) MediaArtwork.Save(Picture(ImageFormat.Png, i), directory);
        Check(Directory.GetFiles(directory, "cover-*.png").Length <= 9, "Artwork cache bounded to eight generated thumbnails");
        Check(File.ReadAllText(unrelated) == "keep", "Cache pruning leaves unrelated files intact");
        Console.WriteLine("PASS: PNG/JPEG decoding, center crop, smooth circular alpha, cache reuse/bounds, invalid data/dimensions and empty-cover fallback");
    }
}
