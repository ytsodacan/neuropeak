using NeuroPeak.Core;
using NeuroPeak.Perception;
using UnityEngine;

namespace NeuroPeak.Patches
{
    public struct FallDamageSnapshot
    {
        public bool Local;
        public float InjuryBefore;
        public float AirborneSeconds;
    }

    public static class FallDamagePatch
    {
        private const float ReportableInjury = 0.02f;

        public static void Prefix(CharacterMovement __instance, out FallDamageSnapshot __state)
        {
            __state = default(FallDamageSnapshot);

            Character? owner = PeakPatchContext.OwnerOf(__instance);
            if (owner == null || owner != Character.localCharacter) return;

            CharacterAfflictions afflictions = owner.refs.afflictions;
            if (afflictions == null) return;

            __state.Local = true;
            __state.InjuryBefore = afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Injury);
            __state.AirborneSeconds = owner.data != null ? owner.data.sinceGrounded : 0f;
        }

        public static void Postfix(CharacterMovement __instance, FallDamageSnapshot __state)
        {
            if (!__state.Local) return;

            Character? owner = PeakPatchContext.OwnerOf(__instance);
            if (owner == null) return;

            CharacterAfflictions afflictions = owner.refs.afflictions;
            if (afflictions == null) return;

            float injuryNow = afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Injury);
            float taken = injuryNow - __state.InjuryBefore;
            if (taken < ReportableInjury) return;

            int percent = Mathf.RoundToInt(taken * 100f);
            int total = Mathf.RoundToInt(injuryNow * 100f);
            string severity = taken > 0.3f ? "That was a bad landing" : "You hit the ground hard";

            UrgentContext.Report(UrgentContext.FallDamageKey,
                $"{severity} after {__state.AirborneSeconds:0.0} seconds in the air. You took {percent}% injury and are now at {total}% injured, which cuts into your maximum stamina.");
        }
    }
}
