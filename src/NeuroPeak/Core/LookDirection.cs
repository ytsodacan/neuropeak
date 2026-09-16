namespace NeuroPeak.Core
{
    public enum LookDirection
    {
        Left,
        Right,
        Up,
        Down
    }

    public static class LookDirectionExtensions
    {
        public const string Left = "left";
        public const string Right = "right";
        public const string Up = "up";
        public const string Down = "down";

        public static readonly string[] All = { Left, Right, Up, Down };

        public static bool TryParse(string? raw, out LookDirection direction)
        {
            switch (raw?.Trim().ToLowerInvariant())
            {
                case Left:
                    direction = LookDirection.Left;
                    return true;
                case Right:
                    direction = LookDirection.Right;
                    return true;
                case Up:
                    direction = LookDirection.Up;
                    return true;
                case Down:
                    direction = LookDirection.Down;
                    return true;
                default:
                    direction = LookDirection.Right;
                    return false;
            }
        }

        public static bool IsHorizontal(this LookDirection direction)
            => direction == LookDirection.Left || direction == LookDirection.Right;

        public static float NominalSign(this LookDirection direction)
            => direction == LookDirection.Right || direction == LookDirection.Up ? 1f : -1f;
    }
}
