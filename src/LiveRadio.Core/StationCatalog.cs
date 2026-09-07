using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LiveRadio.Core
{
    [DataContract]
    public sealed class CatalogQuery
    {
        [DataMember] public string Name = "";
        [DataMember] public string Country = "TW";
        [DataMember] public bool ExpandAliases = true;
        public string Key => Country + "\n" + Name + "\n" + ExpandAliases;
        public string[] Terms => ExpandAliases && Name.Equals("BCC", StringComparison.OrdinalIgnoreCase)
            ? new[] { Name, "中廣" } : new[] { Name };
        public CatalogQuery Normalize()
        {
            var value = new CatalogQuery { Name = (Name ?? "").Trim(), Country = (Country ?? "").Trim().ToUpperInvariant(), ExpandAliases = ExpandAliases };
            RadioBrowserClient.BuildSearchPath(value.Name, value.Country);
            return value;
        }
    }

    [DataContract]
    public sealed class CatalogCursor
    {
        [DataMember] public int[] Offsets = Array.Empty<int>();
        [DataMember] public bool[] Ended = Array.Empty<bool>();
        [DataMember] public List<RadioStation> Pending = new List<RadioStation>();
        public bool HasMore => Pending.Count > 0 || Ended.Any(e => !e);
    }

    public sealed class CatalogPage
    {
        public RadioStation[] Stations;
        public CatalogCursor Cursor;
        public int InvalidCount;
    }

    public static class CatalogPaging
    {
        // Each operation fetches at most one raw page per term; offsets include invalid/duplicate rows.
        // Unshown rows remain in the cursor, so 50-row UI pages never discard API results.
        public static CatalogPage Read(CatalogQuery query, CatalogCursor previous,
            Func<string, int, RadioStation[]> fetch, CancellationToken token)
        {
            var terms = query.Terms;
            var cursor = new CatalogCursor
            {
                Offsets = previous?.Offsets?.Length == terms.Length ? (int[])previous.Offsets.Clone() : new int[terms.Length],
                Ended = previous?.Ended?.Length == terms.Length ? (bool[])previous.Ended.Clone() : new bool[terms.Length],
                Pending = new List<RadioStation>(previous?.Pending ?? new List<RadioStation>())
            };
            int invalid = 0;
            if (cursor.Pending.Count < 50)
                for (int i = 0; i < terms.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (cursor.Ended[i]) continue;
                    var batch = fetch(terms[i], cursor.Offsets[i]) ?? Array.Empty<RadioStation>();
                    token.ThrowIfCancellationRequested();
                    cursor.Offsets[i] += batch.Length;
                    cursor.Ended[i] = batch.Length < 50;
                    invalid += batch.Count(s => s == null || !s.IsValid);
                    cursor.Pending.AddRange(RadioStation.DistinctValid(batch));
                }
            var unique = RadioStation.DistinctValid(cursor.Pending).ToList();
            var stations = unique.Take(50).ToArray();
            cursor.Pending = unique.Skip(50).ToList();
            return new CatalogPage { Stations = stations, Cursor = cursor, InvalidCount = invalid };
        }
    }

    [DataContract]
    public sealed class CatalogCache
    {
        [DataMember] public CatalogQuery Query;
        [DataMember] public List<RadioStation> Stations = new List<RadioStation>();
        [DataMember] public CatalogCursor Cursor;
        [DataMember] public string UpdatedAt;
    }

    // Main-thread state. Network tasks only return values; cancelled tasks can never publish a result.
    public sealed class StationCatalog : IDisposable
    {
        private readonly Func<CatalogQuery, CatalogCursor, CancellationToken, Task<CatalogPage>> _fetch;
        private CancellationTokenSource _cancel;
        private Task<CatalogPage> _pending;
        private CatalogQuery _requested;
        private bool _append;
        public CatalogCache Cache { get; private set; }
        public CatalogQuery Query { get; private set; }
        public bool Loading => _pending != null;
        public bool Visible { get; private set; }
        private LocalizedText _error = "";
        public string Error => _error.ToString();
        public int Revision { get; private set; }
        public bool CanLoadMore => !Loading && Cache?.Query?.Key == Query.Key && Cache.Cursor?.HasMore == true && Cache.Stations.Count < 1000;

        public StationCatalog(CatalogQuery query, CatalogCache cache,
            Func<CatalogQuery, CatalogCursor, CancellationToken, Task<CatalogPage>> fetch)
        {
            Query = query.Normalize(); Cache = cache; _fetch = fetch;
        }
        public void Enter()
        {
            Visible = true;
            if (!Loading && Cache?.Query?.Key != Query.Key) Request(false);
        }
        public void Leave() { Visible = false; Cancel(); }
        public void Search(CatalogQuery query)
        {
            Query = query.Normalize();
            if (Visible) Request(false);
        }
        public void Refresh() { if (Visible) Request(false); }
        public void More() { if (Visible && CanLoadMore) Request(true); }
        private void Request(bool append)
        {
            Cancel(); _error = ""; _append = append; _requested = Query;
            _cancel = new CancellationTokenSource();
            _pending = _fetch(_requested, append ? Cache.Cursor : null, _cancel.Token);
        }
        public bool Tick()
        {
            if (_pending == null || !_pending.IsCompleted) return false;
            var task = _pending; _pending = null;
            try
            {
                var result = task.GetAwaiter().GetResult();
                Cache = new CatalogCache
                {
                    Query = _requested,
                    Stations = RadioStation.DistinctValid((_append ? Cache.Stations : Enumerable.Empty<RadioStation>()).Concat(result.Stations)).Take(1000).ToList(),
                    Cursor = result.Cursor, UpdatedAt = DateTime.UtcNow.ToString("o")
                };
                Revision++;
                return true;
            }
            catch (OperationCanceledException) { _error = L10n.Message("The directory request timed out. Please try again.", "目錄查詢逾時，請重試。"); }
            catch (Exception) { _error = L10n.Message("The directory is unavailable. Try again; your favorites are still available.", "目錄暫時無法連線，請重試；收藏仍可使用。"); }
            finally { _cancel?.Dispose(); _cancel = null; }
            return false;
        }
        private void Cancel()
        {
            var cancel = _cancel; var pending = _pending;
            _pending = null; _cancel = null;
            cancel?.Cancel();
            if (pending != null) _ = pending.ContinueWith(t => { var ignored = t.Exception; cancel?.Dispose(); }, TaskScheduler.Default);
            else cancel?.Dispose();
        }
        public void Dispose() => Leave();
    }
}
