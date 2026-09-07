using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Audio.Radio;
using Game.SceneFlow;
using Game.UI.InGame;

namespace LiveRadio
{
    // Local mods load before the Paradox playset. Resolve this optional startup
    // dependency only after the active playset has initialized ExtendedRadio.
    internal sealed class ExtendedRadioAdapter : IDisposable
    {
        private const string PatchId = "LiveRadio.NativePanel";
        private readonly Type _radioType, _customType, _harmonyType;
        private readonly List<Action> _unsubscribe = new List<Action>();
        private object _harmony;
        internal static readonly BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        internal static Assembly FindReadyAssembly() => GameManager.instance?.modManager?
            .Where(m => m.state == Game.Modding.ModManager.ModInfo.State.Loaded)
            .SelectMany(m => m.instances).Select(m => m.GetType().Assembly)
            .FirstOrDefault(a => a.GetName().Name == "ExtendedRadio");

        internal ExtendedRadioAdapter(Assembly assembly)
        {
            _radioType = assembly.GetType("ExtendedRadio.ExtendedRadio", true);
            _customType = assembly.GetType("ExtendedRadio.CustomRadios", true);
            _harmonyType = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "0Harmony")
                .GetType("HarmonyLib.Harmony", true);
        }

        internal Radio Radio => (Radio)_radioType.GetField("radio", Members).GetValue(null);
        internal List<string> CustomNames => (List<string>)_customType.GetField("customeRadioChannelsName", Members).GetValue(null);
        internal bool AddNetwork(Radio.RadioNetwork network) => (bool)_customType.GetMethod("AddRadioNetworkToTheGame", Members).Invoke(null, new object[] { network });
        internal bool AddChannel(Radio.RadioChannel channel) => (bool)_customType.GetMethod("AddRadioChannelToTheGame", Members).Invoke(null, new object[] { channel, "" });

        internal void Subscribe(string eventName, Action callback)
        {
            var evt = _radioType.GetEvent(eventName, Members) ?? throw new MissingMemberException(_radioType.FullName, eventName);
            var handler = Delegate.CreateDelegate(evt.EventHandlerType, callback.Target, callback.Method);
            evt.AddEventHandler(null, handler);
            _unsubscribe.Add(() => evt.RemoveEventHandler(null, handler));
        }

        internal void PatchPanel()
        {
            _harmony = Activator.CreateInstance(_harmonyType, PatchId);
            var methodType = _harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", true);
            var patch = _harmonyType.GetMethod("Patch", new[] { typeof(MethodBase), methodType, methodType, methodType, methodType });
            void Prefix(Type target, string name, Type implementation)
            {
                var original = target.GetMethod(name, Members) ?? throw new MissingMethodException(target.FullName, name);
                var prefix = implementation.GetMethod("Prefix", Members);
                patch.Invoke(_harmony, new[] { original, Activator.CreateInstance(methodType, prefix), null, null, null });
            }
            Prefix(typeof(RadioUISystem), "GetClipInfo", typeof(CurrentBroadcastPatch));
            Prefix(typeof(Radio), nameof(Game.Audio.Radio.Radio.NextSong), typeof(NextStationPatch));
            Prefix(typeof(Radio), nameof(Game.Audio.Radio.Radio.PreviousSong), typeof(PreviousStationPatch));
        }

        public void Dispose()
        {
            foreach (var unsubscribe in _unsubscribe) unsubscribe();
            _unsubscribe.Clear();
            if (_harmony != null) _harmonyType.GetMethod("UnpatchAll", new[] { typeof(string) }).Invoke(_harmony, new object[] { PatchId });
            _harmony = null;
        }
    }
}
