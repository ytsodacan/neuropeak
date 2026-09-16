using System.Collections.Generic;
using NeuroPeak.Core;
using UnityEngine;

namespace NeuroPeak.Perception
{
    public static class UrgentContext
    {
        public const string FallDamageKey = "fall-damage";
        public const string StaminaDepletedKey = "stamina-depleted";
        public const string CheckpointKey = "checkpoint";
        public const string ImminentFallKey = "imminent-fall";
        public const string LowStaminaKey = "low-stamina";
        public const string TeammateDownKey = "teammate-down";

        private static readonly Dictionary<string, float> LastSentAt = new Dictionary<string, float>();

        public static void Report(string key, string message)
        {
            float minInterval = NeuroPeakPlugin.Settings?.UrgentContextMinInterval.Value ?? 5f;
            float now = Time.unscaledTime;

            if (LastSentAt.TryGetValue(key, out float last) && now - last < minInterval) return;

            LastSentAt[key] = now;
            NeuroContext.SendUrgent(message);
        }

        public static void Forget(string key) => LastSentAt.Remove(key);

        public static void Reset() => LastSentAt.Clear();
    }
}
