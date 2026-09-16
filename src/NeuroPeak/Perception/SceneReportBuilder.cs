using System.Collections.Generic;
using System.Text;
using NeuroPeak.Core;
using UnityEngine;

namespace NeuroPeak.Perception
{
    public static class SceneReportBuilder
    {
        private const int MaxSurfacesReported = 3;
        private const int MaxTeammatesReported = 4;
        private const float MinSurfaceSeparation = 2.5f;

        public static SceneReport Build(PeakPlayerState state)
        {
            SceneReport report = new SceneReport();
            if (!state.InGame) return report;

            float dropBelow = PeakSurfaceProbe.GroundDistanceBelow(state.Position + Vector3.up * 0.2f);
            List<SurfaceHit> surfaces = FindClimbableSurfaces(state);
            List<string> teammates = DescribeTeammates(state);

            StringBuilder text = new StringBuilder();
            text.Append("## Around you\n");
            text.Append(Posture(state, dropBelow)).Append('\n');
            text.Append(StaminaLine(state)).Append('\n');

            if (surfaces.Count > 0)
            {
                text.Append("Climbable from here:\n");
                foreach (SurfaceHit surface in surfaces)
                {
                    text.Append("- ").Append(surface.Label).Append(' ')
                        .Append(surface.InReach
                            ? "directly in front of you, close enough to grab"
                            : RelativePosition.Describe(state, surface.Point))
                        .Append('\n');
                }
            }
            else
            {
                text.Append("Nothing climbable is within reach of you right now.\n");
            }

            string hazards = HazardLine(state, dropBelow);
            if (hazards.Length > 0) text.Append(hazards).Append('\n');

            if (teammates.Count > 0)
            {
                text.Append("Your team:\n");
                foreach (string teammate in teammates) text.Append("- ").Append(teammate).Append('\n');
            }
            else
            {
                text.Append("Nobody else is close enough to see.\n");
            }

            report.Description = text.ToString().TrimEnd();
            report.Digest = BuildDigest(state, dropBelow, surfaces, teammates);
            report.Meaningful = true;
            return report;
        }

        private static string Posture(PeakPlayerState state, float dropBelow)
        {
            string altitude = state.AltitudeMeters > 0.5f
                ? $" at about {Mathf.RoundToInt(state.AltitudeMeters)} m up"
                : string.Empty;

            string region = state.SegmentName.Length > 0 ? $" in the {Readable(state.SegmentName)}" : string.Empty;

            if (state.Climbing) return $"You are hanging off a rock face{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground below you.";
            if (state.RopeClimbing) return $"You are on a rope{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground below you.";
            if (state.VineClimbing) return $"You are on a vine{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground below you.";
            if (state.HoldingClimbHandle) return $"You are hanging from a handhold{altitude}{region}.";
            if (!state.Grounded && state.SinceGrounded > 0.4f) return $"You are in the air{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground.";
            return $"You are standing on solid ground{altitude}{region}.";
        }

        private static string StaminaLine(PeakPlayerState state)
        {
            int percent = Mathf.RoundToInt(state.StaminaFraction * 100f);
            if (state.OutOfStamina) return "You have no stamina left.";
            if (state.StatusSum > 0.05f)
            {
                return $"Stamina is at {percent}% and your injuries are holding back {Mathf.RoundToInt(state.StatusSum * 100f)}% of your maximum.";
            }

            return $"Stamina is at {percent}%.";
        }

        private static string HazardLine(PeakPlayerState state, float dropBelow)
        {
            List<string> hazards = new List<string>();
            float dangerous = NeuroPeakPlugin.Settings?.DangerousDropMeters.Value ?? 12f;

            if (dropBelow >= dangerous)
            {
                hazards.Add(dropBelow >= PeakSurfaceProbe.MaxGroundProbe
                    ? "the drop below you has no bottom in sight"
                    : $"a {RelativePosition.FormatDistance(dropBelow)} drop straight below you");
            }

            if (state.InFog) hazards.Add("thick fog, you cannot see far");
            if (state.Injury > 0.05f) hazards.Add($"you are injured ({Mathf.RoundToInt(state.Injury * 100f)}%)");
            if (state.PassedOut) hazards.Add("you are passing out");

            return hazards.Count == 0 ? string.Empty : $"Careful: {string.Join(", ", hazards.ToArray())}.";
        }

        private static List<SurfaceHit> FindClimbableSurfaces(PeakPlayerState state)
        {
            List<SurfaceHit> found = new List<SurfaceHit>();
            float radius = NeuroPeakPlugin.Settings?.NearbyScanRadius.Value ?? 7f;

            SurfaceHit inReach = PeakSurfaceProbe.ProbeGrabTarget(state);
            if (inReach.Found)
            {
                found.Add(inReach);
            }

            int count = PeakSurfaceProbe.OverlapNearby(state.HeadPosition, radius, out Collider[] buffer);
            for (int i = 0; i < count && found.Count < MaxSurfacesReported; i++)
            {
                Collider collider = buffer[i];
                if (collider == null) continue;
                if (PeakSurfaceProbe.BelongsToCharacter(collider)) continue;

                Vector3 closest = collider.ClosestPoint(state.HeadPosition);
                Vector3 toSurface = closest - state.HeadPosition;
                if (toSurface.sqrMagnitude < 0.01f) continue;

                if (!Physics.Raycast(state.HeadPosition, toSurface.normalized, out RaycastHit hit, toSurface.magnitude + 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!PeakSurfaceProbe.IsClimbableNormal(hit.normal)) continue;
                if (TooCloseToExisting(found, hit.point)) continue;

                found.Add(new SurfaceHit
                {
                    Found = true,
                    Point = hit.point,
                    Normal = hit.normal,
                    Distance = hit.distance,
                    Label = PeakSurfaceProbe.DescribeCollider(hit.collider)
                });
            }

            return found;
        }

        private static bool TooCloseToExisting(List<SurfaceHit> found, Vector3 point)
        {
            foreach (SurfaceHit existing in found)
            {
                if ((existing.Point - point).sqrMagnitude < MinSurfaceSeparation * MinSurfaceSeparation) return true;
            }

            return false;
        }

        private static List<string> DescribeTeammates(PeakPlayerState state)
        {
            List<string> described = new List<string>();
            List<Character> all = Character.AllCharacters;
            if (all == null) return described;

            Character local = Character.localCharacter;

            for (int i = 0; i < all.Count && described.Count < MaxTeammatesReported; i++)
            {
                Character other = all[i];
                if (other == null || other == local) continue;

                CharacterData data = other.data;
                if (data == null) continue;

                string condition = data.dead ? "dead"
                    : data.fullyPassedOut ? "fully passed out"
                    : data.passedOut ? "passed out"
                    : data.isClimbing || data.isRopeClimbing || data.isVineClimbing ? "climbing"
                    : "on their feet";

                described.Add($"{other.characterName} is {condition}, {RelativePosition.Describe(state, other.transform.position)}");
            }

            return described;
        }

        private static string BuildDigest(PeakPlayerState state, float dropBelow, List<SurfaceHit> surfaces, List<string> teammates)
        {
            StringBuilder digest = new StringBuilder();
            digest.Append(state.SegmentName).Append('|')
                .Append(Bucket(state.AltitudeMeters, 10f)).Append('|')
                .Append(PostureKey(state)).Append('|')
                .Append(Bucket(state.StaminaFraction * 100f, 20f)).Append('|')
                .Append(Bucket(dropBelow, 10f)).Append('|')
                .Append(state.InFog ? 1 : 0).Append('|')
                .Append(Bucket(state.Injury * 100f, 25f)).Append('|')
                .Append(surfaces.Count).Append('|');

            foreach (SurfaceHit surface in surfaces) digest.Append(surface.Label).Append(',');
            digest.Append('|').Append(teammates.Count);
            foreach (string teammate in teammates) digest.Append(teammate).Append(',');

            return digest.ToString();
        }

        private static string PostureKey(PeakPlayerState state)
        {
            if (state.Climbing) return "climb";
            if (state.RopeClimbing) return "rope";
            if (state.VineClimbing) return "vine";
            if (state.HoldingClimbHandle) return "handle";
            if (!state.Grounded && state.SinceGrounded > 0.4f) return "air";
            return "ground";
        }

        private static int Bucket(float value, float size) => Mathf.RoundToInt(value / size);

        private static string Readable(string segmentName)
        {
            switch (segmentName)
            {
                case "TheKiln": return "Kiln";
                case "Peak": return "final stretch to the Peak";
                default: return segmentName;
            }
        }
    }
}
