using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using UnityEngine;

namespace NeuroPeak.Actions
{
    public struct MoveCommand
    {
        public MoveDirection Direction;
        public float Duration;
    }

    public sealed class MoveAction : NeuroActionS<MoveCommand>
    {
        public const string ActionName = "move";

        public override string Name => ActionName;

        protected override string Description =>
            "Walk in a direction relative to where you are looking, for a short burst. " +
            "While climbing, this is the direction your hands reach.";

        protected override JsonSchema? Schema
        {
            get
            {
                JsonSchema schema = QJS.WrapObject(new Dictionary<string, JsonSchema>
                {
                    ["direction"] = QJS.Enum(MoveDirectionExtensions.All),
                    ["duration"] = new JsonSchema
                    {
                        Type = JsonSchemaType.Float,
                        Minimum = PeakIntentDriver.MinMoveDuration,
                        Maximum = PeakIntentDriver.MaxMoveDuration
                    }
                }, false);

                schema.Required = new List<string> { "direction" };
                return schema;
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out MoveCommand? parsedData)
        {
            parsedData = null;

            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            string? rawDirection = ActionDataReader.ReadString(actionData, "direction");
            if (string.IsNullOrEmpty(rawDirection))
            {
                return ExecutionResult.Failure(
                    $"Missing required parameter 'direction'. Valid values are: {string.Join(", ", MoveDirectionExtensions.All)}.");
            }

            if (!MoveDirectionExtensions.TryParse(rawDirection, out MoveDirection direction))
            {
                return ExecutionResult.Failure(
                    $"'{rawDirection}' is not a direction you can move in. Valid values are: {string.Join(", ", MoveDirectionExtensions.All)}.");
            }

            float duration = 0.5f;
            if (ActionDataReader.TryReadFloat(actionData, "duration", out float requested))
            {
                if (requested < PeakIntentDriver.MinMoveDuration || requested > PeakIntentDriver.MaxMoveDuration)
                {
                    return ExecutionResult.Failure(
                        $"'duration' must be between {PeakIntentDriver.MinMoveDuration} and {PeakIntentDriver.MaxMoveDuration} seconds, you sent {requested}.");
                }

                duration = requested;
            }

            if (state.FullyPassedOut)
            {
                return ExecutionResult.Failure("You are passed out and cannot walk.");
            }

            parsedData = new MoveCommand { Direction = direction, Duration = duration };
            return ExecutionResult.Success();
        }

        protected override void Execute(MoveCommand? parsedData)
        {
            if (parsedData == null) return;

            MoveCommand command = parsedData.Value;
            PeakActionGuards.Dispatch(() =>
            {
                PeakIntentDriver? driver = PeakIntentDriver.Instance;
                if (driver == null) return;
                driver.Move(command.Direction, Mathf.Clamp(command.Duration, PeakIntentDriver.MinMoveDuration, PeakIntentDriver.MaxMoveDuration));
            });
        }
    }
}
