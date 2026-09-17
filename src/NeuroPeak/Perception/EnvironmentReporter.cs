using NeuroPeak.Core;
using UnityEngine;

namespace NeuroPeak.Perception
{
    public sealed class EnvironmentReporter : MonoBehaviour
    {
        private const float FallingVelocityThreshold = -7f;
        private const float AirborneGraceSeconds = 0.6f;

        private float _nextScanAt;
        private float _lastAmbientAt = -100f;
        private string _lastDigest = string.Empty;
        private bool _wasInGame;

        private void Update()
        {
            NeuroPeakConfig? settings = NeuroPeakPlugin.Settings;
            if (settings != null && !settings.PerceptionEnabled.Value) return;

            PeakPlayerState state = PeakStateTracker.Current;

            if (!state.InGame)
            {
                if (_wasInGame)
                {
                    _wasInGame = false;
                    _lastDigest = string.Empty;
                    UrgentContext.Reset();
                }

                return;
            }

            _wasInGame = true;

            if (Time.unscaledTime < _nextScanAt) return;
            _nextScanAt = Time.unscaledTime + (settings?.PerceptionScanInterval.Value ?? 0.4f);

            if (state.InRun) ReportUrgentConditions(state, settings);
            ReportAmbientChange(state, settings);
        }

        private void ReportAmbientChange(PeakPlayerState state, NeuroPeakConfig? settings)
        {
            SceneReport report = SceneReportBuilder.Build(state);
            if (!report.Meaningful) return;
            if (report.Digest == _lastDigest) return;

            float minInterval = settings?.AmbientContextMinInterval.Value ?? 6f;
            if (Time.unscaledTime - _lastAmbientAt < minInterval) return;

            _lastDigest = report.Digest;
            _lastAmbientAt = Time.unscaledTime;
            NeuroContext.SendAmbient(report.Description);
        }

        private static void ReportUrgentConditions(PeakPlayerState state, NeuroPeakConfig? settings)
        {
            float dangerousDrop = settings?.DangerousDropMeters.Value ?? 12f;
            float lowStamina = settings?.LowStaminaFraction.Value ?? 0.2f;

            bool airborne = !state.Grounded && state.SinceGrounded > AirborneGraceSeconds && !state.ClimbingAnything;
            if (airborne && state.Velocity.y < FallingVelocityThreshold)
            {
                float drop = PeakSurfaceProbe.GroundDistanceBelow(state.Position + Vector3.up * 0.2f);
                if (drop >= dangerousDrop)
                {
                    UrgentContext.Report(UrgentContext.ImminentFallKey,
                        $"You are falling. There is {RelativePosition.FormatDistance(drop)} of open air under you. Grab something now if you can.");
                }
            }
            else
            {
                UrgentContext.Forget(UrgentContext.ImminentFallKey);
            }

            if (state.ClimbingAnything && state.StaminaFraction <= lowStamina && !state.OutOfStamina)
            {
                UrgentContext.Report(UrgentContext.LowStaminaKey,
                    $"Your grip is going. Stamina is down to {Mathf.RoundToInt(state.StaminaFraction * 100f)}% and you are still on the wall. Find a ledge to stand on.");
            }
            else if (!state.ClimbingAnything)
            {
                UrgentContext.Forget(UrgentContext.LowStaminaKey);
            }

            ReportDownedTeammate(state);
            ReportReachingTeammate(state);
            ReportHunters(state);
        }

        private static void ReportHunters(PeakPlayerState state)
        {
            System.Collections.Generic.List<ThreatReading> threats = PeakThreats.Scan(state);
            bool anySevere = false;

            for (int i = 0; i < threats.Count; i++)
            {
                ThreatReading threat = threats[i];
                if (!threat.Severe) continue;

                anySevere = true;
                float distance = Vector3.Distance(threat.Position, state.Position);
                UrgentContext.Report($"{UrgentContext.HunterKey}:{threat.Description}",
                    $"{char.ToUpperInvariant(threat.Description[0])}{threat.Description.Substring(1)}, {RelativePosition.FormatDistance(distance)} away, {RelativePosition.Bearing(state, threat.Position - state.HeadPosition)}. Get away from it or get somewhere it cannot follow.");
            }

            if (!anySevere) UrgentContext.Forget(UrgentContext.HunterKey);
        }

        private static void ReportReachingTeammate(PeakPlayerState state)
        {
            if (string.IsNullOrEmpty(state.ReachingTeammate))
            {
                UrgentContext.Forget(UrgentContext.HandOutKey);
                return;
            }

            UrgentContext.Report(UrgentContext.HandOutKey,
                $"{state.ReachingTeammate} has their hand out towards you, {RelativePosition.FormatDistance(state.ReachingTeammateDistance)} away. Use `reach` with empty hands to grab them.");
        }

        private static void ReportDownedTeammate(PeakPlayerState state)
        {
            System.Collections.Generic.List<Character> all = Character.AllCharacters;
            if (all == null) return;

            Character local = Character.localCharacter;

            for (int i = 0; i < all.Count; i++)
            {
                Character other = all[i];
                if (other == null || other == local) continue;

                CharacterData data = other.data;
                if (data == null) continue;
                if (!data.passedOut && !data.fullyPassedOut && !data.dead) continue;

                string condition = data.dead ? "died" : "gone down";
                UrgentContext.Report($"{UrgentContext.TeammateDownKey}:{other.characterName}",
                    $"{other.characterName} has {condition}, {RelativePosition.DescribeShort(state, other.transform.position)} from you.");
            }
        }
    }
}
