using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace NeuroPeak.Patches
{
    public static class PeakPatchTargets
    {
        public static MethodBase? InputSampled() =>
            AccessTools.Method(typeof(CharacterInput), "Sample", new[] { typeof(bool) });

        public static MethodBase? FallDamageEvaluated() =>
            AccessTools.Method(typeof(CharacterMovement), "CheckFallDamage");

        public static MethodBase? StaminaSpent() =>
            AccessTools.Method(typeof(Character), "UseStamina", new[] { typeof(float), typeof(bool), typeof(bool) });

        public static MethodBase? CheckpointLit() =>
            AccessTools.Method(typeof(Campfire), "Light_Rpc", new[] { typeof(bool), typeof(float) });

        public static MethodBase? SegmentAdvanced() =>
            AccessTools.Method(typeof(MapHandler), "GoToSegment", new[] { typeof(Segment) });

        public static MethodBase? ClimbStarted() =>
            AccessTools.Method(typeof(CharacterClimbing), "StartClimbRpc", new[] { typeof(Vector3), typeof(Vector3) });

        public static MethodBase? ClimbStopped() =>
            AccessTools.Method(typeof(CharacterClimbing), "StopClimbingRpc", new[] { typeof(float) });
    }
}
