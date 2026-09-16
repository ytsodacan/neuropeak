using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class SprintAction : NeuroActionS<bool>
    {
        public const string ActionName = "sprint";

        public override string Name => ActionName;

        protected override string Description =>
            "Turn sprinting on or off. On the ground you run faster; while holding a wall, sprinting pulls you up into a climbing hop. Both drain stamina.";

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
                return ExecutionResult.Failure("Missing required parameter 'enabled'. Send true to start sprinting or false to stop.");
            }

            if (enabled && !state.FullyConscious)
            {
                return ExecutionResult.Failure("You are barely conscious and cannot sprint.");
            }

            if (enabled && state.OutOfStamina)
            {
                return ExecutionResult.Failure("You have no stamina left to sprint with.");
            }

            if (enabled && state.UsingItem && !state.ClimbingAnything)
            {
                return ExecutionResult.Failure($"You cannot sprint while you are using the {state.HeldItemName}. Put it away first.");
            }

            parsedData = enabled;
            return ExecutionResult.Success();
        }

        protected override void Execute(bool? parsedData)
        {
            bool enabled = parsedData ?? false;
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.SetSprint(enabled));
        }
    }
}
