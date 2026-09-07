using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace LiveRadio.Core
{
    [DataContract]
    public sealed class StationState
    {
        [DataMember] public int Version = 2;
        [DataMember] public List<RadioStation> Favorites = new List<RadioStation>();
        [DataMember] public List<RadioStation> Recent = new List<RadioStation>();
        [DataMember] public List<RadioStation> Results = new List<RadioStation>();
        [DataMember] public CatalogCache Cache;
        [DataMember] public CatalogQuery Query;
    }

    public sealed class StationLibrary
    {
        private readonly string _path;
        private RadioStation _removed;
        private int _removedIndex;
        private DateTime _undoUntil;
        public StationState State { get; }
        private LocalizedText _notice = "";
        public string Notice => _notice.ToString();
        public int Revision { get; private set; }
        public bool CanUndo => _removed != null && DateTime.UtcNow < _undoUntil;
        public StationLibrary(string path, CatalogQuery initial)
        {
            _path = path;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            State = new StationState { Query = initial.Normalize() };
            if (!File.Exists(path)) return;
            try
            {
                using (var input = File.OpenRead(path))
                {
                    if (input.Length > 4 * 1024 * 1024) throw new IOException("Station cache too large.");
                    State = JsonData.Read<StationState>(input) ?? State;
                }
                if (State.Version > 2) throw new IOException("Station data was saved by a newer mod.");
                bool migrate = State.Version < 2;
                State.Favorites = RadioStation.DistinctSupported(State.Favorites).Take(50).ToList();
                State.Recent = RadioStation.DistinctSupported(State.Recent).Take(20).ToList();
                State.Query = (State.Query ?? initial).Normalize();
                if (migrate)
                {
                    // Keep the user's previous file intact before writing the versioned representation.
                    string backup = path + ".v1.bak";
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    var legacy = RadioStation.DistinctValid(State.Results).Take(100).ToList();
                    if (legacy.Count > 0) State.Cache = new CatalogCache { Query = State.Query, Stations = legacy };
                }
                if (State.Cache != null)
                {
                    State.Cache.Query = State.Cache.Query?.Normalize();
                    State.Cache.Stations = RadioStation.DistinctValid(State.Cache.Stations).Take(1000).ToList();
                    // Persisted cursors are optional. Invalid data must not create unbounded API offsets.
                    var cursor = State.Cache.Cursor;
                    if (cursor != null && (cursor.Offsets == null || cursor.Ended == null ||
                        cursor.Offsets.Any(o => o < 0 || o > 100000) || cursor.Offsets.Length != cursor.Ended.Length))
                        State.Cache.Cursor = null;
                    else if (cursor != null) cursor.Pending = RadioStation.DistinctValid(cursor.Pending).Take(100).ToList();
                }
                State.Results = new List<RadioStation>(); State.Version = 2;
                if (migrate) Save();
            }
            catch (Exception)
            {
                // Preserve unreadable input for recovery instead of overwriting it on the next favorite.
                string backup = path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak";
                File.Copy(path, backup);
                State = new StationState { Query = initial.Normalize() };
                _notice = L10n.Message("The previous station list could not be read. A backup was kept; you can add favorites again.", "舊清單無法讀取，已保留備份。可重新加入收藏。");
            }
        }
        public static bool Same(RadioStation a, RadioStation b) => a != null && b != null &&
            (string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) || a.StreamUri?.AbsoluteUri == b.StreamUri?.AbsoluteUri);
        public bool IsFavorite(RadioStation station) => State.Favorites.Any(s => Same(s, station));
        public RadioStation AddCustom(string name, string url, string format)
        {
            var station = RadioStation.CreateCustom(name, url, format);
            if (IsFavorite(station)) throw new ArgumentException(L10n.T("This URL is already in your favorites.", "此網址已在收藏中。"));
            if (State.Favorites.Count >= 50) throw new ArgumentException(L10n.T("You can save up to 50 favorites. Remove one first.", "最多收藏 50 台，請先移除一台。"));
            State.Favorites.Add(station);
            if (!Save())
            {
                State.Favorites.Remove(station); Revision++;
                throw new IOException(Notice);
            }
            _notice = L10n.Message("Saved favorite: {0}", "已收藏：{0}", station.Name);
            return station;
        }
        public bool Toggle(RadioStation station)
        {
            if (station == null || !station.IsSupported) { _notice = L10n.Message("This format cannot be saved to favorites.", "目前無法收藏此格式。"); return false; }
            var index = State.Favorites.FindIndex(s => Same(s, station));
            if (index >= 0)
            {
                _removed = State.Favorites[index]; _removedIndex = index; _undoUntil = DateTime.UtcNow.AddSeconds(10);
                State.Favorites.RemoveAt(index); _notice = L10n.Message("Removed favorite: {0}", "已取消收藏：{0}", station.Name);
            }
            else
            {
                if (State.Favorites.Count >= 50) { _notice = L10n.Message("You can save up to 50 favorites. Remove one first.", "最多收藏 50 台，請先移除一台。"); return false; }
                State.Favorites.Add(station); _notice = L10n.Message("Saved favorite: {0}", "已收藏：{0}", station.Name);
            }
            Save(); return true;
        }
        public bool Undo()
        {
            if (!CanUndo || State.Favorites.Count >= 50 || IsFavorite(_removed)) return false;
            State.Favorites.Insert(Math.Min(_removedIndex, State.Favorites.Count), _removed);
            _notice = L10n.Message("Restored favorite: {0}", "已復原收藏：{0}", _removed.Name); _removed = null; Save(); return true;
        }
        public void RecordSuccess(RadioStation station)
        {
            if (station == null || !station.IsSupported) return;
            State.Recent.RemoveAll(s => Same(s, station));
            State.Recent.Insert(0, station);
            State.Recent = State.Recent.Take(20).ToList(); Save();
        }
        public bool Save()
        {
            Revision++;
            try
            {
                string temporary = _path + ".tmp";
                using (var output = File.Create(temporary)) JsonData.Write(output, State);
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                return true;
            }
            catch (Exception) { _notice = L10n.Message("Could not save the station list. Changes may be lost after restarting.", "清單暫時無法儲存，這次變更可能在重啟後遺失。"); return false; }
        }
    }
}
