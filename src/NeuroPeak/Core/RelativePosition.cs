using UnityEngine;

namespace NeuroPeak.Core
{
    public static class RelativePosition
    {
        private const float LevelBand = 1.2f;
        private const float SideBand = 0.4f;

        public static string Describe(PeakPlayerState state, Vector3 target)
        {
            Vector3 offset = target - state.HeadPosition;
            float distance = offset.magnitude;
            return $"{Bearing(state, offset)}, {Elevation(offset.y)}, {FormatDistance(distance)}";
        }

        public static string DescribeShort(PeakPlayerState state, Vector3 target)
        {
            Vector3 offset = target - state.HeadPosition;
            return $"{Bearing(state, offset)} {FormatDistance(offset.magnitude)}";
        }

        public static string Bearing(PeakPlayerState state, Vector3 offset)
        {
            Vector3 flatOffset = new Vector3(offset.x, 0f, offset.z);
            if (flatOffset.sqrMagnitude < 0.04f) return "right where you are";

            Vector3 forward = state.LookFlat.sqrMagnitude > 0.0001f ? state.LookFlat.normalized : Vector3.forward;
            Vector3 right = state.LookRight.sqrMagnitude > 0.0001f ? state.LookRight.normalized : Vector3.right;

            Vector3 flatDirection = flatOffset.normalized;
            float ahead = Vector3.Dot(flatDirection, forward);
            float side = Vector3.Dot(flatDirection, right);

            string depth = ahead >= 0.3f ? "ahead" : ahead <= -0.3f ? "behind" : string.Empty;
            string lateral = side >= SideBand ? "to your right" : side <= -SideBand ? "to your left" : string.Empty;

            if (depth.Length > 0 && lateral.Length > 0) return $"{depth} and {lateral}";
            if (depth.Length > 0) return depth;
            if (lateral.Length > 0) return lateral;
            return "beside you";
        }

        public static string Elevation(float verticalOffset)
        {
            if (verticalOffset > LevelBand) return $"{FormatDistance(verticalOffset)} above";
            if (verticalOffset < -LevelBand) return $"{FormatDistance(-verticalOffset)} below";
            return "level with you";
        }

        public static string Compass(Vector3 flatDirection)
        {
            Vector3 flat = new Vector3(flatDirection.x, 0f, flatDirection.z);
            if (flat.sqrMagnitude < 0.0001f) return "nowhere in particular";

            float heading = Mathf.Repeat(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 360f);
            string[] points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            int index = Mathf.RoundToInt(heading / 45f) % points.Length;
            return $"{points[index]} ({Mathf.RoundToInt(heading)} degrees)";
        }

        public static string CompassOnly(Vector3 flatDirection)
        {
            Vector3 flat = new Vector3(flatDirection.x, 0f, flatDirection.z);
            if (flat.sqrMagnitude < 0.0001f) return "nowhere";

            float heading = Mathf.Repeat(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 360f);
            string[] points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            return points[Mathf.RoundToInt(heading / 45f) % points.Length];
        }

        public static string DescribeWithCoordinates(PeakPlayerState state, Vector3 target)
        {
            return $"{Describe(state, target)}, at {Coordinates(target)}";
        }

        public static string Coordinates(Vector3 position)
        {
            return $"x {Mathf.RoundToInt(position.x)}, y {Mathf.RoundToInt(position.y)}, z {Mathf.RoundToInt(position.z)}";
        }

        public static string FormatDistance(float metres)
        {
            if (metres < 1f) return "under a metre";
            return $"{Mathf.RoundToInt(metres)} m";
        }
    }
}
