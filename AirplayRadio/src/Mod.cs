using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.PSI.Environment;
using Game;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine;

namespace AirplayRadio
{
    public sealed class Mod : IMod
    {
        internal static readonly ILog Log = LogManager.GetLogger("AirplayRadio").SetShowsErrorsInUI(false);
        internal static RadioController Controller;
        internal static Action RestartReceiver;
        internal static LocalizedText Status = L10n.Message("Waiting for ExtendedRadio", "等待 ExtendedRadio 載入");
        internal static string Diagnostics = "";
        private GameObject _host;
        private Settings _settings;
        private bool _failed;
        private float _next;
        public void OnLoad(UpdateSystem updateSystem)
        {
            RestartReceiver = () =>
            {
                if (Controller != null) Controller.Retry();
                else { _failed = false; _next = 0; }
            };
            _settings = new Settings(this);
            AssetDatabase.global.LoadSettings("AirplayRadio", _settings, new Settings(this));
            _settings.RegisterInOptionsUI();
            var localization = GameManager.instance.localizationManager;
            L10n.Locale = localization.activeLocaleId;
            foreach (string locale in localization.GetSupportedLocales()) localization.AddSource(locale, new Locale(_settings, L10n.IsChinese(locale)));
            _host = new GameObject("AirplayRadio");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<Driver>().Owner = this;
            Log.Info("Airplay Radio 0.3.1 loaded. Native receiver runs in-process only while selected.");
        }
        internal void Tick()
        {
            L10n.Locale = GameManager.instance?.localizationManager?.activeLocaleId ?? "en-US";
            if (_failed) return;
            try
            {
                if (Controller == null && Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + 1;
                    var assembly = ExtendedRadioAdapter.FindReadyAssembly();
                    if (assembly == null) return;
                    string data = Path.Combine(EnvPath.kUserDataPath, "ModsData", "AirplayRadio");
                    Directory.CreateDirectory(data);
                    string identityPath = Path.Combine(data, "device-id.txt");
                    string identity;
                    if (File.Exists(identityPath)) identity = File.ReadAllText(identityPath).Trim();
                    else
                    {
                        byte[] id = Guid.NewGuid().ToByteArray().Take(6).ToArray(); id[0] = (byte)((id[0] | 2) & 254);
                        identity = string.Join(":", id.Select(b => b.ToString("X2"))); File.WriteAllText(identityPath, identity);
                    }
                    if (!GameManager.instance.modManager.TryGetExecutableAsset(this, out var executableAsset))
                        throw new InvalidOperationException("The game could not locate the Airplay Radio mod asset");
                    string dll = ModPaths.NativeReceiver(executableAsset.path);
                    Log.Info("Native receiver resolved from mod asset: " + dll);
                    Controller = new RadioController(_host, new ExtendedRadioAdapter(assembly), dll, identity, Path.Combine(data, "receiver.key"), _settings);
                    Controller.Initialize();
                }
                Controller?.Tick();
            }
            catch (Exception ex)
            {
                _failed = true; Status = L10n.Message("Airplay Radio failed. See Logs/AirplayRadio.log.", "Airplay Radio 啟動失敗，請查看 Logs/AirplayRadio.log。"); Log.Error(ex, Status.ToString());
                Controller?.Dispose(); Controller = null;
            }
        }
        public void OnDispose()
        {
            RestartReceiver = null;
            Controller?.Dispose(); Controller = null;
            _settings?.UnregisterInOptionsUI();
            if (_host != null) UnityEngine.Object.Destroy(_host);
        }
    }
    public sealed class Driver : MonoBehaviour
    {
        internal Mod Owner;
        private void Update() => Owner?.Tick();
    }
}
