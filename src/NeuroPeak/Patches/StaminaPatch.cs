using NeuroPeak.Perception;

namespace NeuroPeak.Patches
{
    public static class StaminaPatch
    {
        private const float EmptyThreshold = 0.005f;

        private static bool _wasEmpty;

        public static void Postfix(Character __instance)
        {
            if (__instance == null) return;
            if (__instance != Character.localCharacter) return;

            CharacterData data = __instance.data;
            if (data == null) return;

            bool empty = data.currentStamina < EmptyThreshold && data.extraStamina < EmptyThreshold;

            if (empty && !_wasEmpty)
            {
                _wasEmpty = true;
                string situation = data.isClimbing || data.isRopeClimbing || data.isVineClimbing
                    ? "You are out of stamina while hanging on. Your grip will fail in a moment, get onto something solid."
                    : "You are out of stamina. Stand still on solid ground for a few seconds to get it back.";

                UrgentContext.Report(UrgentContext.StaminaDepletedKey, situation);
                return;
            }

            if (!empty && _wasEmpty)
            {
                _wasEmpty = false;
                UrgentContext.Forget(UrgentContext.StaminaDepletedKey);
            }
        }
    }
}
