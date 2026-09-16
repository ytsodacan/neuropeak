using NeuroPeak.Core;

namespace NeuroPeak.Patches
{
    public static class InputSamplePatch
    {
        public static void Postfix(CharacterInput __instance, bool playerMovementActive)
        {
            if (!playerMovementActive) return;

            NeuroPeakConfig? settings = NeuroPeakPlugin.Settings;
            if (settings != null && !settings.MovementEnabled.Value) return;

            PeakIntentDriver? driver = PeakIntentDriver.Instance;
            if (driver == null) return;

            Character local = Character.localCharacter;
            if (local == null || local.input != __instance) return;

            driver.ApplyTo(local, __instance);
        }
    }
}
