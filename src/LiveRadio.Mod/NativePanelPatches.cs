using Game.Audio.Radio;
using Game.UI.InGame;

namespace LiveRadio
{
    internal static class CurrentBroadcastPatch
    {
        private static bool Prefix(Radio radio, ref RadioUISystem.ClipInfo __result)
        {
            var controller = Mod.Controller;
            if (controller == null || !controller.IsLiveChannel(radio.currentChannel?.name) || radio.hasEmergency) return true;
            __result = controller.GetClipInfo();
            return false;
        }
    }

    internal static class NextStationPatch
    {
        private static bool Prefix(Radio __instance) => !(Mod.Controller?.StepStation(__instance, 1) ?? false);
    }

    internal static class PreviousStationPatch
    {
        private static bool Prefix(Radio __instance) => !(Mod.Controller?.StepStation(__instance, -1) ?? false);
    }
}
