using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public struct LookCommand
    {
        public LookDirection Direction;
        public float Degrees;
    }

    public sealed class LookAction : NeuroActionS<LookCommand>
    {
        public const string ActionName = "look";
        public const float DefaultDegrees = 45f;

        public override string Name => ActionName;

        protected override string Description =>
            "Turn your head to look around. Do this to face a wall before grabbing it, or to see what is above and below you.";

        protected override JsonSchema? Schema
        {
            get
            {
                JsonSchema schema = QJS.WrapObject(new Dictionary<string, JsonSchema>
                {
                    ["direction"] = QJS.Enum(LookDirectionExtensions.All),
                    ["degrees"] = new JsonSchema
                    {
                        Type = JsonSchemaType.Float,
                        Minimum = PeakIntentDriver.MinLookDegrees,
                        Maximum = PeakIntentDriver.MaxLookDegrees
                    }
                }, false);

                schema.Required = new List<string> { "direction" };
                return schema;
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out LookCommand? parsedData)
        {
            parsedData = null;

            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (!state.FullyConscious)
            {
                return ExecutionResult.Failure("You are barely conscious and cannot look around.");
            }

            string? rawDirection = ActionDataReader.ReadString(actionData, "direction");
            if (string.IsNullOrEmpty(rawDirection))
            {
                return ExecutionResult.Failure(
                    $"Missing required parameter 'direction'. Valid values are: {string.Join(", ", LookDirectionExtensions.All)}.");
            }

            if (!LookDirectionExtensions.TryParse(rawDirection, out LookDirection direction))
            {
                return ExecutionResult.Failure(
                    $"'{rawDirection}' is not a direction you can look in. Valid values are: {string.Join(", ", LookDirectionExtensions.All)}.");
            }

            float degrees = DefaultDegrees;
            if (ActionDataReader.TryReadFloat(actionData, "degrees", out float requested))
            {
                if (requested < PeakIntentDriver.MinLookDegrees || requested > PeakIntentDriver.MaxLookDegrees)
                {
                    return ExecutionResult.Failure(
                        $"'degrees' must be between {PeakIntentDriver.MinLookDegrees} and {PeakIntentDriver.MaxLookDegrees}, you sent {requested}.");
                }

                degrees = requested;
            }

            parsedData = new LookCommand { Direction = direction, Degrees = degrees };
            return ExecutionResult.Success();
        }

        protected override void Execute(LookCommand? parsedData)
        {
            if (parsedData == null) return;

            LookCommand command = parsedData.Value;
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.Look(command.Direction, command.Degrees));
        }
    }
}
