using System;
using System.Reflection;
using HarmonyLib;

namespace NeuroPeak.Patches
{
    public static class PeakPatchInstaller
    {
        public static void ApplyAll(Harmony harmony)
        {
            Apply(harmony, "player input sampling", PeakPatchTargets.InputSampled(), typeof(InputSamplePatch), null, nameof(InputSamplePatch.Postfix));
            Apply(harmony, "fall damage", PeakPatchTargets.FallDamageEvaluated(), typeof(FallDamagePatch), nameof(FallDamagePatch.Prefix), nameof(FallDamagePatch.Postfix));
            Apply(harmony, "stamina use", PeakPatchTargets.StaminaSpent(), typeof(StaminaPatch), null, nameof(StaminaPatch.Postfix));
            Apply(harmony, "checkpoint campfire", PeakPatchTargets.CheckpointLit(), typeof(CheckpointPatch), null, nameof(CheckpointPatch.Postfix));
            Apply(harmony, "segment change", PeakPatchTargets.SegmentAdvanced(), typeof(SegmentAdvancedPatch), null, nameof(SegmentAdvancedPatch.Postfix));
        }

        private static void Apply(Harmony harmony, string label, MethodBase? target, Type patchType, string? prefixName, string? postfixName)
        {
            if (target == null)
            {
                NeuroPeakPlugin.Log?.LogWarning(
                    $"Could not find the PEAK method for {label}. That hook is disabled. Update PeakPatchTargets after decompiling the current Assembly-CSharp.dll.");
                return;
            }

            try
            {
                HarmonyMethod? prefix = Resolve(patchType, prefixName);
                HarmonyMethod? postfix = Resolve(patchType, postfixName);
                harmony.Patch(target, prefix, postfix);
                NeuroPeakPlugin.Log?.LogDebug($"Patched {target.DeclaringType?.Name}.{target.Name} for {label}");
            }
            catch (Exception e)
            {
                NeuroPeakPlugin.Log?.LogError($"Failed to patch {label}: {e}");
            }
        }

        private static HarmonyMethod? Resolve(Type patchType, string? methodName)
        {
            if (methodName == null) return null;

            MethodInfo? method = AccessTools.Method(patchType, methodName);
            return method == null ? null : new HarmonyMethod(method);
        }
    }
}
