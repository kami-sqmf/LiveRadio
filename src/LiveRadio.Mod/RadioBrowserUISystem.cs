using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.UI.Binding;
using Game.UI;
using Game.UI.InGame;
using LiveRadio.Core;
using Unity.Entities;
using UnityEngine;

namespace LiveRadio
{
    public sealed class RadioBrowserUISystem : UISystemBase
    {
        private RawValueBinding _data, _playback;
        private ValueBinding<int> _openRequest;
        private ValueBinding<string> _locale, _manualResult;
        private int _revision = -1, _openCount;
        private bool _dirty = true;
        private float _nextUpdate;
        private string _error = "";

        protected override void OnCreate()
        {
            base.OnCreate();
            AddBinding(_data = new RawValueBinding("LiveRadio", "data", WriteData));
            AddBinding(_playback = new RawValueBinding("LiveRadio", "playback", WritePlayback));
            AddBinding(_openRequest = new ValueBinding<int>("LiveRadio", "openRequest", 0));
            AddBinding(_locale = new ValueBinding<string>("LiveRadio", "locale", L10n.Locale));
            AddBinding(_manualResult = new ValueBinding<string>("LiveRadio", "manualResult", ""));
            AddBinding(new TriggerBinding<string, string, string>("LiveRadio", "addCustom", (name, url, format) =>
            {
                try
                {
                    if (Mod.Controller == null) throw new InvalidOperationException(Mod.BootstrapStatus);
                    Mod.Controller.AddCustom(name, url, format);
                    _manualResult.Update("saved:" + Guid.NewGuid().ToString());
                }
                catch (Exception ex) { _manualResult.Update("error:" + Guid.NewGuid().ToString() + ":" + ex.Message); }
                _dirty = true;
            }));
            AddBinding(new TriggerBinding("LiveRadio", "recheckDecoder", () => Run(c => c.RecheckDecoder())));
            AddBinding(new TriggerBinding("LiveRadio", "icons", () => Run(c => c.LoadIcons())));
            AddBinding(new TriggerBinding("LiveRadio", "decoderHelp", () => Application.OpenURL("https://ffmpeg.org/download.html#build-windows")));
            AddBinding(new TriggerBinding<string>("LiveRadio", "tab", tab => Run(c =>
            {
                if (tab == "explore") c.Catalog.Enter(); else c.Catalog.Leave();
            })));
            AddBinding(new TriggerBinding<string, string, bool>("LiveRadio", "search", (name, country, aliases) => Run(c =>
            {
                c.Catalog.Search(new CatalogQuery { Name = name, Country = country, ExpandAliases = aliases });
                c.Library.State.Query = c.Catalog.Query; c.Library.Save();
            })));
            AddBinding(new TriggerBinding("LiveRadio", "refresh", () => Run(c => c.Catalog.Refresh())));
            AddBinding(new TriggerBinding("LiveRadio", "more", () => Run(c => c.Catalog.More())));
            AddBinding(new TriggerBinding<string>("LiveRadio", "play", id => Run(c => c.Play(id))));
            AddBinding(new TriggerBinding<string>("LiveRadio", "favorite", id => Run(c => c.ToggleFavorite(id))));
            AddBinding(new TriggerBinding("LiveRadio", "undo", () => Run(c => c.UndoFavorite())));
            AddBinding(new TriggerBinding("LiveRadio", "enableRadio", () => Run(c => c.EnableRadio())));
            AddBinding(new TriggerBinding("LiveRadio", "close", () => Run(c => c.Catalog.Leave())));
            AddBinding(new TriggerBinding<string>("LiveRadio", "uiLog", message => Mod.Log.Info("UI: " + (message ?? "").Substring(0, Math.Min(240, message?.Length ?? 0)))));
        }

        private void Run(Action<RadioController> action)
        {
            try { _error = ""; if (Mod.Controller != null) action(Mod.Controller); }
            catch (Exception ex) { _error = L10n.T("Action failed: {0}", "操作未完成：{0}", ex.Message); Mod.Log.Warn(_error); }
            _dirty = true;
        }

        public static void OpenBrowser()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            world.GetOrCreateSystemManaged<GamePanelUISystem>().ShowPanel(new RadioPanel());
            var ui = world.GetOrCreateSystemManaged<RadioBrowserUISystem>();
            ui._openRequest.Update(++ui._openCount);
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (UnityEngine.Time.unscaledTime < _nextUpdate) return;
            _nextUpdate = UnityEngine.Time.unscaledTime + 0.25f;
            if (_locale.value != L10n.Locale) { _locale.Update(L10n.Locale); _dirty = true; }
            var controller = Mod.Controller;
            int revision = controller?.Library.Revision ?? -1;
            if (_dirty || revision != _revision)
            {
                _dirty = false; _revision = revision; _data.Update();
            }
            _playback.Update();
        }

        private static void Text(IJsonWriter writer, string name, string value)
        { writer.PropertyName(name); writer.Write(value ?? ""); }
        private static void Bool(IJsonWriter writer, string name, bool value)
        { writer.PropertyName(name); writer.Write(value); }
        private void WriteData(IJsonWriter writer)
        {
            var c = Mod.Controller;
            writer.TypeBegin("LiveRadio.Data");
            WriteStations(writer, "favorites", c?.Library.State.Favorites);
            WriteStations(writer, "recent", c?.Library.State.Recent);
            WriteStations(writer, "results", c?.Catalog.Cache?.Stations);
            Text(writer, "query", c?.Catalog.Query.Name);
            Text(writer, "country", c?.Catalog.Query.Country ?? "TW");
            Bool(writer, "aliases", c?.Catalog.Query.ExpandAliases ?? true);
            Text(writer, "cacheQuery", c?.Catalog.Cache?.Query?.Name);
            Text(writer, "cacheCountry", c?.Catalog.Cache?.Query?.Country);
            Text(writer, "updatedAt", c?.Catalog.Cache?.UpdatedAt);
            writer.TypeEnd();
        }
        private static void WriteStations(IJsonWriter writer, string name, IEnumerable<RadioStation> source)
        {
            var stations = source?.ToArray() ?? Array.Empty<RadioStation>();
            writer.PropertyName(name); writer.ArrayBegin(stations.Length);
            foreach (var station in stations)
            {
                writer.TypeBegin("LiveRadio.Station");
                Text(writer, "id", station.Id); Text(writer, "name", station.Name);
                Text(writer, "country", station.CountryCode); Text(writer, "codec", station.Codec);
                Text(writer, "tags", station.Tags); Text(writer, "language", station.Language);
                Text(writer, "url", station.StreamUri?.AbsoluteUri);
                // Gameface's resource handler rejects HTTP (including HTTPS redirects).
                // Only expose artwork validated and downloaded by our cache.
                Text(writer, "icon", Mod.Controller?.IconFor(station));
                writer.PropertyName("bitrate"); writer.Write(station.Bitrate);
                Bool(writer, "supported", station.IsSupported);
                Bool(writer, "custom", station.Custom);
                Bool(writer, "needsDecoder", station.Format != StreamFormat.Mp3);
                Text(writer, "reason", station.IsSupported ? "" : L10n.T("This audio format is not supported.", "目前不支援此音訊格式"));
                writer.TypeEnd();
            }
            writer.ArrayEnd();
        }
        private void WritePlayback(IJsonWriter writer)
        {
            var c = Mod.Controller;
            writer.TypeBegin("LiveRadio.Playback");
            Bool(writer, "ready", c?.Ready ?? false);
            Text(writer, "id", c?.SelectedId); Text(writer, "station", c?.SelectedName);
            Text(writer, "state", c?.PlaybackState ?? "native");
            Text(writer, "status", c != null && c.SelectedId.Length > 0 ? c.GetClipInfo().info : L10n.T("Use the native radio controls for playback, pause and volume.", "使用原生電台控制播放、暫停與音量"));
            Bool(writer, "paused", c?.Paused ?? true); Bool(writer, "muted", c?.Muted ?? false);
            Bool(writer, "enabled", c?.RadioEnabled ?? false); Bool(writer, "emergency", c?.Emergency ?? false);
            Bool(writer, "decoder", c?.DecoderAvailable ?? false);
            writer.PropertyName("volume"); writer.Write(c?.Volume ?? 0);
            Bool(writer, "loading", c?.Catalog.Loading ?? false);
            Bool(writer, "more", c?.Catalog.CanLoadMore ?? false);
            Bool(writer, "undo", c?.Library.CanUndo ?? false);
            Text(writer, "searchError", c?.Catalog.Error);
            Text(writer, "notice", _error.Length > 0 ? _error : !string.IsNullOrEmpty(c?.Notice) ? c.Notice : c?.Library.Notice);
            Text(writer, "bootstrap", c == null ? Mod.BootstrapStatus : "");
            writer.TypeEnd();
        }
    }
}
