using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace LiveRadio.Core
{
    public enum StreamFormat { Unsupported, Mp3, Aac, Ogg, Hls, Auto }

    [DataContract]
    public sealed class RadioStation
    {
        [DataMember(Name = "stationuuid")] public string Id { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "url_resolved")] public string ResolvedUrl { get; set; }
        [DataMember(Name = "url")] public string Url { get; set; }
        [DataMember(Name = "countrycode")] public string CountryCode { get; set; }
        [DataMember(Name = "codec")] public string Codec { get; set; }
        [DataMember(Name = "bitrate")] public int Bitrate { get; set; }
        [DataMember(Name = "hls")] public int Hls { get; set; }
        [DataMember(Name = "tags")] public string Tags { get; set; }
        [DataMember(Name = "language")] public string Language { get; set; }
        [DataMember(Name = "favicon", EmitDefaultValue = false)] public string Favicon { get; set; }
        [DataMember(Name = "custom", EmitDefaultValue = false)] public bool Custom { get; set; }

        public bool IsValid => Guid.TryParse(Id, out _) && !string.IsNullOrWhiteSpace(Name) && StreamUri != null;

        public Uri StreamUri
        {
            get
            {
                var candidate = string.IsNullOrWhiteSpace(ResolvedUrl) ? Url : ResolvedUrl;
                return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
                       string.IsNullOrEmpty(uri.UserInfo) ? uri : null;
            }
        }

        public StreamFormat Format
        {
            get
            {
                if (Hls != 0 || StreamUri?.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) == true) return StreamFormat.Hls;
                switch ((Codec ?? "").Trim().ToUpperInvariant().Replace("-", "").Replace(" ", ""))
                {
                    case "MP3": return StreamFormat.Mp3;
                    case "AAC": case "AAC+": case "AACPLUS": case "HEAAC": case "HEAACV2": return StreamFormat.Aac;
                    case "OGG": case "VORBIS": case "OPUS": case "OGGVORBIS": case "OGGOPUS": return StreamFormat.Ogg;
                    case "HLS": return StreamFormat.Hls;
                    case "AUTO": return Custom ? StreamFormat.Auto : StreamFormat.Unsupported;
                    default: return StreamFormat.Unsupported;
                }
            }
        }

        public bool IsSupported => IsValid && Format != StreamFormat.Unsupported;

        public static RadioStation CreateCustom(string name, string url, string format)
        {
            name = (name ?? "").Trim(); url = (url ?? "").Trim();
            if (name.Length == 0 || name.Length > 100 || name.Any(char.IsControl))
                throw new ArgumentException(L10n.T("Enter a station name (1–100 characters).", "請輸入電台名稱（1–100 字）。"));
            var station = new RadioStation { Id = Guid.NewGuid().ToString(), Name = name, Url = url, Custom = true };
            if (url.Length > 4096 || url.Any(char.IsControl) || station.StreamUri == null || !string.IsNullOrEmpty(station.StreamUri.Fragment))
                throw new ArgumentException(L10n.T("Enter a direct HTTP(S) stream URL without a username, password or fragment.", "請輸入 HTTP(S) 串流直連網址，不可包含帳號、密碼或片段識別符號。"));
            format = (format ?? "AUTO").ToUpperInvariant();
            if (!new[] { "AUTO", "MP3", "AAC", "OGG", "HLS" }.Contains(format))
                throw new ArgumentException(L10n.T("Choose Auto, MP3, AAC, OGG or HLS.", "請選擇自動、MP3、AAC、OGG 或 HLS。"));
            if (format == "AUTO")
            {
                switch (Path.GetExtension(station.StreamUri.AbsolutePath).ToLowerInvariant())
                {
                    case ".mp3": format = "MP3"; break;
                    case ".aac": format = "AAC"; break;
                    case ".ogg": case ".opus": format = "OGG"; break;
                    case ".m3u8": format = "HLS"; break;
                }
            }
            station.Codec = format; station.Hls = format == "HLS" ? 1 : 0;
            return station;
        }

        public static IEnumerable<RadioStation> DistinctValid(IEnumerable<RadioStation> stations) =>
            (stations ?? Enumerable.Empty<RadioStation>()).Where(s => s != null && s.IsValid)
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .GroupBy(s => s.StreamUri.AbsoluteUri, StringComparer.Ordinal).Select(g => g.First());

        public static IEnumerable<RadioStation> DistinctSupported(IEnumerable<RadioStation> stations) =>
            (stations ?? Enumerable.Empty<RadioStation>()).Where(s => s != null && s.IsSupported)
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First())
                .GroupBy(s => s.StreamUri.AbsoluteUri, StringComparer.Ordinal).Select(g => g.First());
    }

    public static class JsonData
    {
        public static T Read<T>(Stream stream) => (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        public static void Write<T>(Stream stream, T value) => new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
    }
}
