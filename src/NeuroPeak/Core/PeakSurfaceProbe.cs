using UnityEngine;

namespace NeuroPeak.Core
{
    public struct SurfaceHit
    {
        public bool Found;
        public bool InReach;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public string Label;
    }

    public static class PeakSurfaceProbe
    {
        public const float GrabReach = 1.25f;
        public const float MaxGroundProbe = 120f;

        private const float OverhangTolerance = 80f;
        private const float SlopeTolerance = 40f;

        private static readonly Collider[] OverlapBuffer = new Collider[64];
        private static readonly RaycastHit[] RayBuffer = new RaycastHit[32];

        public static bool IsClimbableNormal(Vector3 normal)
        {
            float angleFromUp = Vector3.Angle(normal, Vector3.up);
            float offset = angleFromUp - 90f;
            return offset > 0f ? Mathf.Abs(offset) <= OverhangTolerance : Mathf.Abs(offset) <= SlopeTolerance;
        }

        public static SurfaceHit ProbeGrabTarget(PeakPlayerState state)
        {
            Vector3 origin = PeakStateTracker.EyePosition(state);
            Vector3 direction = state.LookDirection.sqrMagnitude > 0.0001f ? state.LookDirection.normalized : Vector3.forward;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                float radius = attempt * 0.05f;
                if (TryCast(origin, direction, GrabReach, radius, out RaycastHit hit))
                {
                    return new SurfaceHit
                    {
                        Found = IsClimbableNormal(hit.normal),
                        InReach = true,
                        Point = hit.point,
                        Normal = hit.normal,
                        Distance = hit.distance,
                        Label = DescribeCollider(hit.collider)
                    };
                }
            }

            return default(SurfaceHit);
        }

        public static float GroundDistanceBelow(Vector3 from)
        {
            return Physics.Raycast(from, Vector3.down, out RaycastHit hit, MaxGroundProbe, ~0, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : MaxGroundProbe;
        }

        public static int OverlapNearby(Vector3 center, float radius, out Collider[] buffer)
        {
            buffer = OverlapBuffer;
            return Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
        }

        public static bool BelongsToCharacter(Collider collider)
        {
            return collider != null && collider.GetComponentInParent<Character>() != null;
        }

        public static string DescribeCollider(Collider collider)
        {
            if (collider == null) return "surface";

            string raw = collider.transform.name.ToLowerInvariant();
            if (raw.Contains("rope")) return "rope";
            if (raw.Contains("vine")) return "vine";
            if (raw.Contains("ladder")) return "ladder";
            if (raw.Contains("root")) return "root";
            if (raw.Contains("ledge")) return "ledge";
            if (raw.Contains("rock") || raw.Contains("cliff") || raw.Contains("stone")) return "rock face";
            if (raw.Contains("ice") || raw.Contains("snow")) return "icy surface";
            if (raw.Contains("tree") || raw.Contains("branch")) return "tree";
            return "climbable surface";
        }

        public static bool IsTerrainHit(Collider collider)
        {
            if (collider == null) return false;
            if (collider.GetComponentInParent<Character>() != null) return false;
            if (collider.GetComponentInParent<Item>() != null) return false;
            return true;
        }

        private static bool TryCast(Vector3 origin, Vector3 direction, float distance, float radius, out RaycastHit hit)
        {
            int count = radius <= 0f
                ? Physics.RaycastNonAlloc(origin, direction, RayBuffer, distance, ~0, QueryTriggerInteraction.Ignore)
                : Physics.SphereCastNonAlloc(origin, radius, direction, RayBuffer, distance, ~0, QueryTriggerInteraction.Ignore);

            hit = default(RaycastHit);
            float nearest = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = RayBuffer[i];
                if (!IsTerrainHit(candidate.collider)) continue;
                if (candidate.distance >= nearest) continue;

                nearest = candidate.distance;
                hit = candidate;
                found = true;
            }

            return found;
        }
    }
}
