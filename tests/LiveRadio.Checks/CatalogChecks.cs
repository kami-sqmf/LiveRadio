using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiveRadio.Core;

internal static class CatalogChecks
{
    private static RadioStation Make(int i, string name = "Station") => new RadioStation
    { Id = new Guid(i, 0, 0, new byte[8]).ToString(), Name = name, Url = "https://example.com/" + i, Codec = "MP3" };

    internal static async Task Run(Action<bool, string> assert)
    {
        var query = new CatalogQuery { Name = "bcc", Country = " tw " }.Normalize();
        assert(query.Terms.SequenceEqual(new[] { "bcc", "中廣" }) && query.Country == "TW", "BCC alias retains the original search and expands to 中廣");
        query.ExpandAliases = false;
        assert(query.Terms.Length == 1, "Alias expansion can be explicitly disabled");
        query = new CatalogQuery();
        int pageCalls = 0;
        RadioStation[] Fetch(string term, int offset) { pageCalls++; return Enumerable.Range(offset + 1, 50).Select(i => Make(i)).ToArray(); }
        var first = CatalogPaging.Read(query, null, Fetch, CancellationToken.None);
        var second = CatalogPaging.Read(query, first.Cursor, Fetch, CancellationToken.None);
        var third = CatalogPaging.Read(query, second.Cursor, Fetch, CancellationToken.None);
        assert(pageCalls == 3 && third.Cursor.Offsets[0] == 150 && third.Stations[0].Url.EndsWith("/101"), "Explorer advances raw API offsets beyond the old 100-station cap");
        var alias = new CatalogQuery { Name = "BCC" };
        var merged = CatalogPaging.Read(alias, null, (term, offset) => Enumerable.Range(term == "BCC" ? 1 : 51, 50).Select(i => Make(i)).ToArray(), CancellationToken.None);
        var pending = CatalogPaging.Read(alias, merged.Cursor, (term, offset) => throw new Exception("Pending rows should not fetch"), CancellationToken.None);
        assert(merged.Stations.Length == 50 && pending.Stations.Length == 50 && pending.Stations[0].Url.EndsWith("/51"), "Alias pages preserve unshown rows without extra requests or lost stations");
        assert(merged.Cursor.Pending.Count == 50, "Reading a cursor does not mutate the previously committed page");

        int requests = 0;
        var old = new TaskCompletionSource<CatalogPage>();
        var latest = new TaskCompletionSource<CatalogPage>();
        using (var catalog = new StationCatalog(query, null, (q, c, token) => ++requests == 1 ? old.Task : latest.Task))
        {
            catalog.Tick(); catalog.Leave(); catalog.Tick();
            assert(requests == 0 && !catalog.Loading, "Startup, empty Favorites and repeated local views make zero directory requests");
            catalog.Enter(); assert(requests == 1 && catalog.Loading, "Entering Explore explicitly starts the first directory request");
            catalog.Search(new CatalogQuery { Name = "new" });
            latest.SetResult(new CatalogPage { Stations = new[] { Make(500, "new") }, Cursor = new CatalogCursor() });
            catalog.Tick();
            old.SetResult(new CatalogPage { Stations = new[] { Make(501, "old") }, Cursor = new CatalogCursor() });
            catalog.Tick();
            assert(catalog.Cache.Stations.Single().Name == "new", "A late stale response cannot replace a newer search result");
            catalog.Leave(); catalog.Enter();
            assert(requests == 2, "Reopening Explore with the same cached query does not refetch");
        }
        var detached = new TaskCompletionSource<CatalogPage>();
        using (var catalog = new StationCatalog(query, null, (q, c, token) => detached.Task))
        {
            catalog.Enter(); catalog.Leave(); detached.SetResult(first); catalog.Tick();
            assert(catalog.Cache == null && !catalog.Loading, "Leaving Explore cancels publication even if the network ignores cancellation");
        }
        using (var catalog = new StationCatalog(query, new CatalogCache { Query = query, Stations = first.Stations.ToList(), Cursor = first.Cursor },
            (q, c, token) => Task.FromException<CatalogPage>(new IOException("offline"))))
        {
            catalog.Enter(); catalog.Refresh(); catalog.Tick();
            assert(catalog.Cache.Stations.Count == 50 && catalog.Error.Length > 0, "Directory failure preserves cached stations with a separate error");
        }

        string directory = Path.Combine(Path.GetTempPath(), "LiveRadio-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var station = Make(1, "中廣測試");
            string file = Path.Combine(directory, "stations.json");
            using (var stream = new MemoryStream())
            {
                JsonData.Write(stream, new[] { station });
                string rows = Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(file, "{\"Favorites\":" + rows + ",\"Results\":" + rows + "}");
            }
            var library = new StationLibrary(file, query);
            assert(library.State.Favorites.Single().Name == "中廣測試" && library.State.Version == 2 && File.Exists(file + ".v1.bak"), "Legacy favorites migrate with a recoverable original backup");
            assert(library.State.Cache.Stations.Count == 1 && library.State.Favorites.Count == 1, "Legacy search results become cache, not additional favorites");
            library.Toggle(Make(2));
            assert(library.State.Favorites.Count == 2 && library.State.Recent.Count == 0, "Favoriting does not require playback or add recent history");
            library.Toggle(station); library.Undo();
            assert(library.State.Favorites[0].Id == station.Id, "Undo restores a removed favorite at its original position");
            for (int i = 3; i < 51; i++) library.Toggle(Make(i));
            bool overflow = library.Toggle(Make(70));
            assert(!overflow && library.State.Favorites.Count == 50, "Favorite limit is enforced without dropping existing stations");
            for (int i = 1; i <= 23; i++) library.RecordSuccess(Make(i));
            library.RecordSuccess(Make(8));
            assert(library.State.Recent.Count == 20 && library.State.Recent[0].Id == Make(8).Id && library.State.Recent.Count(s => s.Id == Make(8).Id) == 1,
                "Successful recent history is bounded, unique and ordered by latest use");
            var reloaded = new StationLibrary(file, query);
            assert(reloaded.State.Favorites.Count == 50 && reloaded.State.Recent.Count == 20, "Favorites and recent history survive a restart");
            File.WriteAllText(file, "{truncated");
            var recovered = new StationLibrary(file, query);
            assert(Directory.GetFiles(directory, "*.unreadable-*.bak").Length == 1 && recovered.Notice.Length > 0, "Unreadable cache is preserved before recovery");
        }
        finally { Directory.Delete(directory, true); }
        await Task.CompletedTask;
    }
}
