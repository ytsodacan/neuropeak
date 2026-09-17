using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class CrouchAction : NeuroActionS<bool>
    {
        public const string ActionName = "crouch";

        public override string Name => ActionName;

        protected override string Description =>
            "Crouch down or stand back up. Crouching keeps you steadier on narrow ledges and lets you fit under things.";

        protected override JsonSchema? Schema => QJS.WrapObject(new Dictionary<string, JsonSchema>
        {
            ["enabled"] = QJS.Type(JsonSchemaType.Boolean)
        });

        protected override ExecutionResult Validate(ActionJData actionData, out bool? parsedData)
        {
            parsedData = null;

            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (!ActionDataReader.TryReadBool(actionData, "enabled", out bool enabled))
            {
                return ExecutionResult.Failure("Missing required parameter 'enabled'. Send true to crouch or false to stand up.");
            }

            if (enabled && state.ClimbingAnything)
            {
                return ExecutionResult.Failure("You cannot crouch while hanging off a wall.");
            }

            parsedData = enabled;
            return ExecutionResult.Success();
        }

        protected override void Execute(bool? parsedData)
        {
            bool enabled = parsedData ?? false;
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.SetCrouch(enabled));
        }
    }
}
