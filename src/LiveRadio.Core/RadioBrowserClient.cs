using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRadio.Core
{
    public sealed class StationSearchResult
    {
        public RadioStation[] Stations { get; internal set; }
        public int HlsCount { get; internal set; }
        public int OtherUnsupportedCount { get; internal set; }
        public bool Limited { get; internal set; }
    }

    public sealed class RadioBrowserClient
    {
        public const string UserAgent = "LiveRadio-CS2/0.4.4";
        public const int ResultLimit = 100;
        private const int PageSize = 100;
        private string _lastServer;

        public static string BuildSearchPath(string name, string country, int offset = 0, int limit = PageSize)
        {
            country = (country ?? "").Trim().ToUpperInvariant();
            if (country.Length != 0 && (country.Length != 2 || country.Any(c => c < 'A' || c > 'Z')))
                throw new ArgumentException(L10n.T("Enter a two-letter country code, such as TW, or leave it empty for worldwide search.", "國家代碼請填兩碼，例如 TW；留空可搜尋全球。"));
            name = (name ?? "").Trim();
            if (name.Length > 100) throw new ArgumentException(L10n.T("Search text cannot exceed 100 characters.", "搜尋文字最多 100 字。"));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (limit < 1 || limit > PageSize) throw new ArgumentOutOfRangeException(nameof(limit));
            return "/json/stations/search?hidebroken=true&order=clickcount&reverse=true&limit=" + limit + "&offset=" + offset +
                "&name=" + Uri.EscapeDataString(name) + (country.Length == 0 ? "" : "&countrycode=" + country);
        }

        public Task<CatalogPage> BrowseAsync(CatalogQuery query, CatalogCursor cursor, CancellationToken token)
        {
            BuildSearchPath(query.Name, query.Country);
            return Task.Run(() =>
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    Exception lastError = null;
                    foreach (string host in DiscoverServers())
                    {
                        timeout.Token.ThrowIfCancellationRequested();
                        try
                        {
                            var page = CatalogPaging.Read(query, cursor,
                                (name, offset) => Get<RadioStation[]>(host, BuildSearchPath(name, query.Country, offset, 50), timeout.Token), timeout.Token);
                            _lastServer = host;
                            return page;
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException)) { lastError = ex; }
                    }
                    token.ThrowIfCancellationRequested();
                    throw new IOException(L10n.T("The directory is unavailable. Try again; your favorites are still available.", "目錄暫時無法連線，請重試；收藏仍可使用。"), lastError);
                }
            }, token);
        }

        public async Task<RadioStation[]> SearchAsync(string name, string country, CancellationToken token) =>
            (await SearchDetailedAsync(name, country, token).ConfigureAwait(false)).Stations;

        public Task<StationSearchResult> SearchDetailedAsync(string name, string country, CancellationToken token)
        {
            BuildSearchPath(name, country);
            return Task.Run(() =>
            {
                Exception lastError = null;
                foreach (string host in DiscoverServers())
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var result = ReadPages(offset => Get<RadioStation[]>(host, BuildSearchPath(name, country, offset), token), token);
                        _lastServer = host;
                        return result;
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException)) { lastError = ex; }
                }
                token.ThrowIfCancellationRequested();
                throw new IOException(L10n.T("Radio Browser is unavailable. Please try again later.", "Radio Browser 暫時無法連線，請稍後重試。"), lastError);
            }, token);
        }

        internal static StationSearchResult ReadPages(Func<int, RadioStation[]> fetch, CancellationToken token)
        {
            var candidates = new List<RadioStation>();
            var result = new StationSearchResult();
            for (int page = 0; page < 5; page++)
            {
                token.ThrowIfCancellationRequested();
                var batch = fetch(page * PageSize) ?? Array.Empty<RadioStation>();
                candidates.AddRange(batch.Where(s => s != null));
                var supported = RadioStation.DistinctSupported(candidates).ToArray();
                result.Stations = supported.Take(ResultLimit).ToArray();
                result.Limited = supported.Length > ResultLimit || batch.Length >= PageSize;
                if (supported.Length >= ResultLimit || batch.Length < PageSize) break;
            }
            var unsupported = candidates.Where(s => !s.IsSupported).GroupBy(s => s.Id).Select(g => g.First()).ToArray();
            result.HlsCount = unsupported.Count(s => s.Hls != 0);
            result.OtherUnsupportedCount = unsupported.Length - result.HlsCount;
            return result;
        }

        public Task CountSelectionAsync(string id, CancellationToken token)
        {
            if (!Guid.TryParse(id, out _)) return Task.CompletedTask;
            string host = _lastServer ?? "de1.api.radio-browser.info";
            return Task.Run(() =>
            {
                // The API asks clients to record actual station selections, never retries or list views.
                try { Get<object>(host, "/json/url/" + Uri.EscapeDataString(id), token, parse: false); }
                catch { /* Listening must not depend on the usage counter. */ }
            }, token);
        }

        public string GetFavicon(string id, CancellationToken token)
        {
            if (!Guid.TryParse(id, out _)) return "";
            var station = Get<RadioStation[]>(_lastServer ?? "de1.api.radio-browser.info", "/json/stations/byuuid/" + id, token)?.FirstOrDefault();
            return station?.Favicon ?? "";
        }

        private IEnumerable<string> DiscoverServers()
        {
            var hosts = new List<string>();
            if (_lastServer != null) hosts.Add(_lastServer);
            try
            {
                foreach (var address in Dns.GetHostAddresses("all.api.radio-browser.info").Take(4))
                {
                    try
                    {
                        string host = Dns.GetHostEntry(address).HostName;
                        if (host.EndsWith(".api.radio-browser.info", StringComparison.OrdinalIgnoreCase)) hosts.Add(host);
                    }
                    catch (System.Net.Sockets.SocketException) { }
                }
            }
            catch (System.Net.Sockets.SocketException) { }
            hosts.Add("de1.api.radio-browser.info");
            hosts.Add("nl1.api.radio-browser.info");
            return hosts.Distinct(StringComparer.OrdinalIgnoreCase).Take(4);
        }

        private static T Get<T>(string host, string path, CancellationToken token, bool parse = true)
        {
#pragma warning disable SYSLIB0014
            var request = (HttpWebRequest)WebRequest.Create("https://" + host + path);
#pragma warning restore SYSLIB0014
            request.UserAgent = UserAgent;
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            request.Accept = "application/json";
            using (token.Register(request.Abort))
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var source = response.GetResponseStream())
            using (var content = new MemoryStream())
            {
                var bytes = new byte[8192];
                int read;
                while ((read = source.Read(bytes, 0, bytes.Length)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (content.Length + read > 1024 * 1024) throw new IOException("API response exceeded 1 MiB.");
                    content.Write(bytes, 0, read);
                }
                content.Position = 0;
                return parse ? JsonData.Read<T>(content) : default;
            }
        }
    }
}
