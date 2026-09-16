using UnityEngine;

namespace NeuroPeak.Core
{
    public enum MoveDirection
    {
        Forward,
        Backward,
        Left,
        Right
    }

    public static class MoveDirectionExtensions
    {
        public const string Forward = "forward";
        public const string Backward = "backward";
        public const string Left = "left";
        public const string Right = "right";

        public static readonly string[] All = { Forward, Backward, Left, Right };

        public static bool TryParse(string? raw, out MoveDirection direction)
        {
            switch (raw?.Trim().ToLowerInvariant())
            {
                case Forward:
                    direction = MoveDirection.Forward;
                    return true;
                case Backward:
                    direction = MoveDirection.Backward;
                    return true;
                case Left:
                    direction = MoveDirection.Left;
                    return true;
                case Right:
                    direction = MoveDirection.Right;
                    return true;
                default:
                    direction = MoveDirection.Forward;
                    return false;
            }
        }

        public static Vector2 ToInput(this MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Backward: return new Vector2(0f, -1f);
                case MoveDirection.Left: return new Vector2(-1f, 0f);
                case MoveDirection.Right: return new Vector2(1f, 0f);
                default: return new Vector2(0f, 1f);
            }
        }
    }
}
