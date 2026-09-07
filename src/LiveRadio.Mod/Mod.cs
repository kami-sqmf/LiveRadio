using System;
using System.IO;
using LiveRadio.Core;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.PSI.Environment;
using Game;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine;

namespace LiveRadio
{
    public sealed class Mod : IMod
    {
        internal static readonly ILog Log = LogManager.GetLogger("LiveRadio").SetShowsErrorsInUI(false);
        internal static RadioController Controller;
        private Settings _settings;
        private GameObject _host;
        private ExtendedRadioAdapter _adapter;
        private bool _failed;
        private float _nextDependencyCheck;
        private static LocalizedText _bootstrapStatus = L10n.Message("Waiting for ExtendedRadio. Make sure it is enabled in the active playset.", "等待 ExtendedRadio 載入；請確認目前播放集已啟用它。");

        internal static string BootstrapStatus => _bootstrapStatus.ToString();

        public void OnLoad(UpdateSystem updateSystem)
        {
            try
            {
                _settings = new Settings(this);
                AssetDatabase.global.LoadSettings("LiveRadio", _settings, new Settings(this));
                _settings.RegisterInOptionsUI();
                var localization = GameManager.instance.localizationManager;
                L10n.Locale = localization.activeLocaleId;
                foreach (string locale in localization.GetSupportedLocales())
                    localization.AddSource(locale, new Locale(_settings, L10n.IsChinese(locale)));
                _host = new GameObject("LiveRadio");
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _host.AddComponent<RadioDriver>().Owner = this;
                updateSystem.UpdateAt<RadioBrowserUISystem>(SystemUpdatePhase.UIUpdate);
                Log.Info("LiveRadio bootstrap loaded; waiting for the active playset's ExtendedRadio.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LiveRadio could not initialize. ExtendedRadio must be enabled in the active playset.");
                OnDispose();
            }
        }

        internal void Tick()
        {
            L10n.Locale = GameManager.instance?.localizationManager?.activeLocaleId ?? "en-US";
            if (_failed) return;
            try
            {
                if (Controller == null && Time.unscaledTime >= _nextDependencyCheck)
                {
                    _nextDependencyCheck = Time.unscaledTime + 1f;
                    var assembly = ExtendedRadioAdapter.FindReadyAssembly();
                    if (assembly == null) return;
                    _adapter = new ExtendedRadioAdapter(assembly);
                    Controller = new RadioController(_host, Path.Combine(EnvPath.kUserDataPath, "ModsData", "LiveRadio"), _adapter, () => _settings.EffectiveFfmpegPath, _settings.SearchName, _settings.CountryCode);
                    _adapter.PatchPanel();
                    Controller.Initialize();
                    Log.Info("LiveRadio 0.4.4 connected: local favorites, lazy explorer, shared native radio controls. No startup directory query.");
#if LIVERADIO_DIAGNOSTICS
                    Log.Info("DIAGNOSTIC BUILD: native decoder probe available in Options > Live Radio.");
#endif
                }
                Controller?.Tick();
            }
            catch (Exception ex)
            {
                _failed = true;
                _bootstrapStatus = L10n.Message("Native radio integration failed. See Logs/LiveRadio.log.", "原生電台整合失敗，請查看 Logs/LiveRadio.log。");
                Log.Error(ex, BootstrapStatus);
                try { Controller?.Dispose(); }
                finally { Controller = null; _adapter?.Dispose(); _adapter = null; }
            }
        }

        public void OnDispose()
        {
            Controller?.Dispose();
            Controller = null;
            _adapter?.Dispose();
            _adapter = null;
            _settings?.UnregisterInOptionsUI();
            if (_host != null) UnityEngine.Object.Destroy(_host);
            _host = null;
            Log.Info("LiveRadio disposed.");
        }
    }

    public sealed class RadioDriver : MonoBehaviour
    {
        internal Mod Owner;
        private void Update() => Owner?.Tick();
    }
}
