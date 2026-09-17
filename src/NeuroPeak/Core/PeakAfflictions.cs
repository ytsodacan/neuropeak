using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroPeak.Core
{
    public struct AfflictionReading
    {
        public string Name;
        public float Amount;
    }

    public static class PeakAfflictions
    {
        public const float Noticeable = 0.03f;

        private static readonly Dictionary<string, string> Advice = new Dictionary<string, string>
        {
            ["Hunger"] = "eat something",
            ["Injury"] = "use a medical item",
            ["Cold"] = "get warm, a campfire or a torch helps",
            ["Hot"] = "get out of the heat",
            ["Poison"] = "use an antidote",
            ["Drowsy"] = "use something that wakes you up",
            ["Curse"] = "find something that lifts curses",
            ["Weight"] = "drop something heavy",
            ["Thorns"] = "get the thorns pulled out",
            ["Spores"] = "get clear of the spores",
            ["Web"] = "break out of the web",
            ["Petrify"] = "you are turning to stone",
            ["Crab"] = "shake the crab off",
            ["FlyTrap"] = "get out of the flytrap",
            ["Arrow"] = "pull the arrow out"
        };

        public static List<AfflictionReading> Read(Character character)
        {
            List<AfflictionReading> readings = new List<AfflictionReading>();

            try
            {
                CharacterAfflictions afflictions = character.refs.afflictions;
                if (afflictions == null) return readings;

                foreach (CharacterAfflictions.STATUSTYPE status in Enum.GetValues(typeof(CharacterAfflictions.STATUSTYPE)))
                {
                    float amount = afflictions.GetCurrentStatus(status);
                    if (amount < Noticeable) continue;

                    readings.Add(new AfflictionReading { Name = status.ToString(), Amount = amount });
                }
            }
            catch (Exception)
            {
            }

            readings.Sort((a, b) => b.Amount.CompareTo(a.Amount));
            return readings;
        }

        public static string Describe(List<AfflictionReading> readings)
        {
            if (readings == null || readings.Count == 0) return string.Empty;

            List<string> parts = new List<string>();
            for (int i = 0; i < readings.Count; i++)
            {
                AfflictionReading reading = readings[i];
                int percent = Mathf.RoundToInt(reading.Amount * 100f);
                string advice = Advice.TryGetValue(reading.Name, out string tip) ? $" — {tip}" : string.Empty;
                parts.Add($"{reading.Name} {percent}%{advice}");
            }

            return string.Join("; ", parts.ToArray());
        }
    }
}
