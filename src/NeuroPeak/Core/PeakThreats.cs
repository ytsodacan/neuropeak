using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NeuroPeak.Core
{
    public struct ThreatReading
    {
        public string Description;
        public Vector3 Position;
        public bool Severe;
    }

    public static class PeakThreats
    {
        public const float ScanRadius = 30f;
        public const int MaxReported = 5;

        private static readonly Dictionary<string, string> Hunters = new Dictionary<string, string>
        {
            ["Scoutmaster"] = "the Scoutmaster is hunting you",
            ["BotBoar"] = "a boar that will charge you",
            ["BeeSwarm"] = "a swarm of bees",
            ["VenusFlyTrap"] = "a flytrap that swallows people",
            ["Beehive"] = "a beehive, disturb it and the bees come out",
            ["Snowball"] = "a rolling snowball",
            ["SpikeRoller"] = "a spike roller",
            ["LavaRising"] = "rising lava"
        };

        private static readonly string[] StatusFieldNames = { "statusType", "addtlStatus", "statusToAddOnRemove", "afflictionType" };

        private static readonly Dictionary<Type, FieldInfo[]> StatusFieldCache = new Dictionary<Type, FieldInfo[]>();

        public static List<ThreatReading> Scan(PeakPlayerState state)
        {
            List<ThreatReading> found = new List<ThreatReading>();
            List<Transform> seen = new List<Transform>();

            Collider[] buffer;
            int count = PeakSurfaceProbe.OverlapNearby(state.Position, ScanRadius, out buffer);

            for (int i = 0; i < count && found.Count < MaxReported; i++)
            {
                Collider collider = buffer[i];
                if (collider == null) continue;
                if (PeakSurfaceProbe.BelongsToCharacter(collider)) continue;

                ThreatReading reading = Classify(collider);
                if (reading.Description.Length == 0) continue;

                Transform root = collider.transform;
                if (seen.Contains(root)) continue;

                seen.Add(root);
                found.Add(reading);
            }

            found.Sort((a, b) => b.Severe.CompareTo(a.Severe));
            return found;
        }

        private static ThreatReading Classify(Collider collider)
        {
            ThreatReading reading = default(ThreatReading);
            reading.Description = string.Empty;

            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null) continue;

                if (Hunters.TryGetValue(behaviour.GetType().Name, out string hunter))
                {
                    reading.Description = hunter;
                    reading.Position = behaviour.transform.position;
                    reading.Severe = true;
                    return reading;
                }

                if (reading.Description.Length > 0) continue;

                string status = ReadStatus(behaviour);
                if (status.Length == 0) continue;

                reading.Description = $"{CleanName(behaviour.gameObject.name)} — touching it gives you {status}";
                reading.Position = behaviour.transform.position;
            }

            return reading;
        }

        private static string ReadStatus(MonoBehaviour behaviour)
        {
            Type type = behaviour.GetType();

            if (!StatusFieldCache.TryGetValue(type, out FieldInfo[] fields))
            {
                List<FieldInfo> matches = new List<FieldInfo>();
                foreach (string name in StatusFieldNames)
                {
                    FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null && field.FieldType == typeof(CharacterAfflictions.STATUSTYPE)) matches.Add(field);
                }

                fields = matches.ToArray();
                StatusFieldCache[type] = fields;
            }

            if (fields.Length == 0) return string.Empty;

            List<string> names = new List<string>();
            for (int i = 0; i < fields.Length; i++)
            {
                try
                {
                    object value = fields[i].GetValue(behaviour);
                    if (value == null) continue;

                    string name = value.ToString();
                    if (!names.Contains(name)) names.Add(name);
                }
                catch (Exception)
                {
                }
            }

            return names.Count == 0 ? string.Empty : string.Join(" and ", names.ToArray());
        }

        private static string CleanName(string raw)
        {
            int clone = raw.IndexOf("(Clone)", StringComparison.Ordinal);
            string trimmed = clone >= 0 ? raw.Substring(0, clone) : raw;
            return trimmed.Replace('_', ' ').Trim();
        }
    }
}
