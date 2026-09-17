using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class ReachAction : NeuroActionS<float>
    {
        public const string ActionName = "reach";
        public const float MinHold = 0.2f;
        public const float MaxHold = 10f;
        public const float DefaultHold = 3f;

        public override string Name => ActionName;

        protected override string Description =>
            "Put your hand out towards a teammate so you can grab them or they can grab you. Use it when someone is reaching for you, or to haul someone up. Your hands must be empty.";

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
                return ExecutionResult.Failure("You are barely conscious and cannot reach for anyone.");
            }

            if (state.HoldingItem)
            {
                return ExecutionResult.Failure(
                    $"Your hands are full of {state.HeldItemName}. Use 'select_slot' with 0 to put it away before reaching out.");
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

            if (!PeakReaching.AnyoneWithinReach(state))
            {
                return ExecutionResult.Success(
                    "Reaching out, but nobody is close enough to take your hand yet. Get nearer to them.");
            }

            return ExecutionResult.Success(
                string.IsNullOrEmpty(state.ReachingTeammate)
                    ? "Reaching out."
                    : $"Reaching out towards {state.ReachingTeammate}.");
        }

        protected override void Execute(float? parsedData)
        {
            float hold = parsedData ?? DefaultHold;
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.Reach(hold));
        }
    }
}
