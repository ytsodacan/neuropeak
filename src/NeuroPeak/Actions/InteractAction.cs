using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class InteractAction : NeuroActionS<float>
    {
        public const string ActionName = "interact";
        public const float MinHold = 0.1f;
        public const float MaxHold = 10f;
        public const float DefaultHold = 0.3f;

        public override string Name => ActionName;

        protected override string Description =>
            "Interact with whatever you are looking at: pick up an item, open a chest, light a campfire, or help a teammate up. Some things need holding, so pass hold_seconds for those.";

        protected override JsonSchema? Schema
        {
            get
            {
                JsonSchema schema = QJS.WrapObject(new Dictionary<string, JsonSchema>
                {
                    ["hold_seconds"] = new JsonSchema
                    {
                        Type = JsonSchemaType.Float,
                        Minimum = MinHold,
                        Maximum = MaxHold
                    }
                }, false);

                schema.Required = new List<string>();
                return schema;
            }
        }

        protected override ExecutionResult Validate(ActionJData actionData, out float? parsedData)
        {
            parsedData = null;

            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (!state.FullyConscious)
            {
                return ExecutionResult.Failure("You are barely conscious and cannot interact with anything.");
            }

            if (!state.LookingAtSomething)
            {
                return ExecutionResult.Failure(
                    "You are not looking at anything you can interact with. Use 'look' to turn towards something, or 'move' closer to it.");
            }

            float hold = DefaultHold;
            if (ActionDataReader.TryReadFloat(actionData, "hold_seconds", out float requested))
            {
                if (requested < MinHold || requested > MaxHold)
                {
                    return ExecutionResult.Failure($"'hold_seconds' must be between {MinHold} and {MaxHold}, you sent {requested}.");
                }

                hold = requested;
            }

            parsedData = hold;
            return ExecutionResult.Success($"Interacting with {DescribeTarget(state)}.");
        }

        private static string DescribeTarget(PeakPlayerState state)
        {
            if (!string.IsNullOrEmpty(state.LookingAtName)) return state.LookingAtName;
            return "it";
        }

        protected override void Execute(float? parsedData)
        {
            float hold = parsedData ?? DefaultHold;
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.Interact(hold));
        }
    }
}
