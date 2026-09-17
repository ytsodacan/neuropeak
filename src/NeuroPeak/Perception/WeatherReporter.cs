using NeuroPeak.Core;
using UnityEngine;

namespace NeuroPeak.Perception
{
    public sealed class WeatherReporter : MonoBehaviour
    {
        private const float CheckInterval = 1f;
        private const float StormWarningSeconds = 45f;

        private float _nextCheckAt;
        private string _lastDigest = string.Empty;
        private bool _warnedAboutIncomingStorm;
        private bool _wasDaytime = true;
        private bool _wasStormActive;
        private bool _wasInWindZone;
        private bool _wasInFog;
        private bool _wasRisingHazardStarted;
        private int _lastRisingWarningBand = int.MaxValue;
        private bool _seeded;

        private void Update()
        {
            NeuroPeakConfig? settings = NeuroPeakPlugin.Settings;
            if (settings != null && !settings.PerceptionEnabled.Value) return;
            if (Time.unscaledTime < _nextCheckAt) return;
            _nextCheckAt = Time.unscaledTime + CheckInterval;

            PeakPlayerState state = PeakStateTracker.Current;
            if (!state.InGame || !state.InRun)
            {
                _seeded = false;
                _lastDigest = string.Empty;
                _warnedAboutIncomingStorm = false;
                return;
            }

            WeatherSnapshot weather = PeakWeather.Read(state);
            if (!weather.Known && !weather.StormSystemPresent) return;

            if (!_seeded)
            {
                _seeded = true;
                _wasDaytime = weather.IsDaytime;
                _wasStormActive = weather.StormActive;
                _wasInWindZone = weather.InWindZone;
                _wasInFog = weather.InFog;
                _wasRisingHazardStarted = weather.RisingHazardStarted;
                _lastDigest = weather.Digest;
                NeuroContext.SendAmbient(Opening(weather));
                return;
            }

            ReportTransitions(weather);

            if (weather.Digest != _lastDigest) _lastDigest = weather.Digest;
        }

        private void ReportTransitions(WeatherSnapshot weather)
        {
            if (weather.IsDaytime != _wasDaytime)
            {
                _wasDaytime = weather.IsDaytime;
                NeuroContext.SendUrgent(weather.IsDaytime
                    ? $"The sun is coming up. It is day {weather.DayCount + 1} and you can see where you are going again."
                    : "Night is falling. It gets cold and hard to see up here after dark, so find shelter or keep moving.");
            }

            if (weather.StormActive && !_wasStormActive)
            {
                _wasStormActive = true;
                _warnedAboutIncomingStorm = false;
                NeuroContext.SendUrgent(
                    $"A {weather.StormFlavour} has hit. The wind is tearing across the mountain, it will try to pull you off the rock, and holding on costs far more stamina than usual. Get behind something solid if you can.");
            }
            else if (!weather.StormActive && _wasStormActive)
            {
                _wasStormActive = false;
                NeuroContext.SendUrgent($"The {weather.StormFlavour} has blown itself out. The wind has dropped and climbing is back to normal.");
            }

            if (!weather.StormActive && !_warnedAboutIncomingStorm && weather.StormSystemPresent
                && weather.SecondsUntilStorm > 0f && weather.SecondsUntilStorm <= StormWarningSeconds)
            {
                _warnedAboutIncomingStorm = true;
                NeuroContext.SendUrgent(
                    $"A {weather.StormFlavour} is building, about {Mathf.RoundToInt(weather.SecondsUntilStorm)} seconds out. When it arrives the wind will make climbing much harder, so get somewhere sheltered or finish this stretch now.");
            }

            if (weather.InWindZone != _wasInWindZone)
            {
                _wasInWindZone = weather.InWindZone;
                NeuroContext.SendAmbient(weather.InWindZone
                    ? "You have walked into the exposed, windy part of the mountain."
                    : "You are out of the wind now.");
            }

            ReportRisingHazard(weather);

            if (weather.InFog != _wasInFog)
            {
                _wasInFog = weather.InFog;
                NeuroContext.SendUrgent(weather.InFog
                    ? "The fog has closed in around you. You cannot see far, and staying in it is not safe. Climb out of it."
                    : "You are clear of the fog.");
            }
        }

        private void ReportRisingHazard(WeatherSnapshot weather)
        {
            if (!weather.RisingHazard)
            {
                _lastRisingWarningBand = int.MaxValue;
                _wasRisingHazardStarted = false;
                return;
            }

            if (weather.RisingHazardStarted && !_wasRisingHazardStarted)
            {
                _wasRisingHazardStarted = true;
                NeuroContext.SendUrgent(
                    $"{Capitalise(weather.RisingHazardName)} is rising from below and it will not stop. Everything under it is gone. You have to keep climbing.");
            }

            if (!weather.RisingHazardStarted) return;

            int band = WarningBand(weather.RisingHazardHeightBelow);
            if (band >= _lastRisingWarningBand) return;

            _lastRisingWarningBand = band;

            if (band <= 0)
            {
                NeuroContext.SendUrgent($"{Capitalise(weather.RisingHazardName)} is right underneath you. Climb now or you are dead.");
                return;
            }

            NeuroContext.SendUrgent(
                $"{Capitalise(weather.RisingHazardName)} is only about {Mathf.RoundToInt(weather.RisingHazardHeightBelow)} m below you and still coming up. Keep climbing.");
        }

        private static int WarningBand(float heightBelow)
        {
            if (heightBelow <= 5f) return 0;
            if (heightBelow <= 15f) return 1;
            if (heightBelow <= 35f) return 2;
            if (heightBelow <= 70f) return 3;
            return int.MaxValue;
        }

        private static string Capitalise(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        private static string Opening(WeatherSnapshot weather)
        {
            string time = weather.Known
                ? $"It is {weather.PartOfDay} on day {weather.DayCount + 1}."
                : "You cannot tell what time it is.";

            if (weather.StormActive) return $"{time} A {weather.StormFlavour} is blowing right now and the wind is dangerous.";
            if (weather.InWindZone) return $"{time} You are on an exposed stretch where the wind gets up.";
            return $"{time} The weather is calm for now.";
        }
    }
}
