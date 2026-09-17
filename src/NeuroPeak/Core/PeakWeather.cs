using System;
using UnityEngine;

namespace NeuroPeak.Core
{
    public sealed class WeatherSnapshot
    {
        public static readonly WeatherSnapshot Unknown = new WeatherSnapshot();

        public bool Known;
        public float TimeOfDayNormalized;
        public bool IsDaytime;
        public int DayCount;
        public string PartOfDay = string.Empty;

        public bool StormSystemPresent;
        public bool InWindZone;
        public float StormProgress;
        public float SecondsUntilStorm;
        public bool StormActive;

        public bool InFog;
        public string BiomeName = string.Empty;
        public string StormFlavour = "storm";

        public bool RisingHazard;
        public string RisingHazardName = string.Empty;
        public float RisingHazardHeightBelow;
        public bool RisingHazardStarted;

        public string Digest =>
            $"{PartOfDay}|{(IsDaytime ? 1 : 0)}|{DayCount}|{(InWindZone ? 1 : 0)}|{(StormActive ? 1 : 0)}|{StormBucket}|{(InFog ? 1 : 0)}|{BiomeName}|{(RisingHazard ? Mathf.RoundToInt(RisingHazardHeightBelow / 20f) : -99)}";

        private int StormBucket => SecondsUntilStorm <= 0f ? -1 : Mathf.RoundToInt(SecondsUntilStorm / 30f);
    }

    public static class PeakWeather
    {
        public const float StormActiveThreshold = 0.05f;
        public const float BiomeScanInterval = 5f;

        private static Biome[]? _biomes;
        private static float _lastBiomeScan = -100f;

        public static WeatherSnapshot Read(PeakPlayerState state)
        {
            WeatherSnapshot weather = new WeatherSnapshot { InFog = state.InFog };

            try
            {
                DayNightManager clock = DayNightManager.instance;
                if (clock != null)
                {
                    weather.Known = true;
                    weather.TimeOfDayNormalized = Mathf.Repeat(clock.timeOfDayNormalized, 1f);
                    weather.IsDaytime = clock.isDay > 0.5f;
                    weather.DayCount = clock.dayCount;
                    weather.PartOfDay = NamePartOfDay(weather.TimeOfDayNormalized, weather.IsDaytime);
                }
            }
            catch (Exception)
            {
            }

            try
            {
                WindChillZone storm = WindChillZone.instance;
                if (storm != null)
                {
                    weather.StormSystemPresent = true;
                    weather.InWindZone = storm.localCharacterInsideBounds;
                    weather.StormProgress = storm.StormProgress;
                    weather.SecondsUntilStorm = storm.timeUntilStorm;
                    weather.StormActive = storm.StormProgress > StormActiveThreshold;
                }
            }
            catch (Exception)
            {
            }

            ReadBiome(state, weather);
            ReadRisingHazard(state, weather);

            return weather;
        }

        private static void ReadBiome(PeakPlayerState state, WeatherSnapshot weather)
        {
            try
            {
                Biome nearest = NearestBiome(state.Position);
                weather.BiomeName = nearest != null ? nearest.biomeType.ToString() : state.SegmentName;
            }
            catch (Exception)
            {
                weather.BiomeName = state.SegmentName;
            }

            weather.StormFlavour = FlavourFor(weather.BiomeName);
        }

        private static Biome? NearestBiome(Vector3 position)
        {
            if (Time.unscaledTime - _lastBiomeScan > BiomeScanInterval || _biomes == null)
            {
                _lastBiomeScan = Time.unscaledTime;
                _biomes = UnityEngine.Object.FindObjectsOfType<Biome>();
            }

            if (_biomes == null || _biomes.Length == 0) return null;

            Biome? best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _biomes.Length; i++)
            {
                Biome candidate = _biomes[i];
                if (candidate == null) continue;

                float distance = (candidate.transform.position - position).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private static string FlavourFor(string biome)
        {
            switch (biome)
            {
                case "Tropics":
                case "Swamp":
                case "Grasslands":
                    return "rainstorm";
                case "Alpine":
                case "Peak":
                    return "snowstorm";
                case "Mesa":
                    return "sandstorm";
                case "Volcano":
                case "Hell":
                    return "ash storm";
                default:
                    return "storm";
            }
        }

        private static void ReadRisingHazard(PeakPlayerState state, WeatherSnapshot weather)
        {
            try
            {
                System.Collections.Generic.List<LavaRising> all = LavaRising.ALL_LAVA;
                if (all == null) return;

                for (int i = 0; i < all.Count; i++)
                {
                    LavaRising rising = all[i];
                    if (rising == null || rising.ended) continue;
                    if (rising.lava == null) continue;

                    float below = state.Position.y - rising.lava.position.y;
                    if (below < 0f) below = 0f;

                    weather.RisingHazard = true;
                    weather.RisingHazardStarted = rising.started;
                    weather.RisingHazardHeightBelow = below;
                    weather.RisingHazardName = NameRisingField(rising);
                    return;
                }
            }
            catch (Exception)
            {
            }
        }

        private static string NameRisingField(LavaRising rising)
        {
            try
            {
                switch (rising.risingFieldType)
                {
                    case LavaRising.RisingFieldType.Gloom: return "the gloom";
                    case LavaRising.RisingFieldType.VoidGhosts: return "the void ghosts";
                    default: return "the lava";
                }
            }
            catch (Exception)
            {
                return "something";
            }
        }

        private static string NamePartOfDay(float normalized, bool daytime)
        {
            if (!daytime) return normalized < 0.5f ? "the dead of night" : "the small hours before dawn";
            if (normalized < 0.35f) return "morning";
            if (normalized < 0.5f) return "midday";
            if (normalized < 0.7f) return "afternoon";
            return "evening, the light is going";
        }
    }
}
