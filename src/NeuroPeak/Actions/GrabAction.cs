using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class GrabAction : NeuroActionS<float>
    {
        public const string ActionName = "grab";
        public const float MinHoldSeconds = 0.2f;
        public const float MaxHoldSeconds = 20f;
        public const float DefaultHoldSeconds = 4f;

        public override string Name => ActionName;

        protected override string Description =>
            "Grab the surface you are looking at and hold on to climb it. You must be facing something climbable within arm's reach.";

        protected override JsonSchema? Schema
        {
            get
            {
                JsonSchema schema = QJS.WrapObject(new Dictionary<string, JsonSchema>
                {
                    ["hold_seconds"] = new JsonSchema
                    {
                        Type = JsonSchemaType.Float,
                        Minimum = MinHoldSeconds,
                        Maximum = MaxHoldSeconds
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
                return ExecutionResult.Failure("You are barely conscious and cannot hold onto anything.");
            }

            if (state.Climbing)
            {
                return ExecutionResult.Failure("You are already holding onto the wall.");
            }

            if (state.RopeClimbing || state.VineClimbing)
            {
                return ExecutionResult.Failure("You are already on a rope or vine. Use 'release' before grabbing rock.");
            }

            if (state.HoldingItem)
            {
                return ExecutionResult.Failure(
                    $"You are holding {state.HeldItemName}. Put the item away before you can grab the wall.");
            }

            if (state.OutOfStamina)
            {
                return ExecutionResult.Failure("You are out of stamina and your hands will slip straight off.");
            }

            SurfaceHit target = PeakSurfaceProbe.ProbeGrabTarget(state);
            if (!target.Found)
            {
                return ExecutionResult.Failure(
                    "There is nothing climbable within reach in front of you. Turn towards a wall or move closer first.");
            }

            float hold = DefaultHoldSeconds;
            if (ActionDataReader.TryReadFloat(actionData, "hold_seconds", out float requested))
            {
                if (requested < MinHoldSeconds || requested > MaxHoldSeconds)
                {
                    return ExecutionResult.Failure(
                        $"'hold_seconds' must be between {MinHoldSeconds} and {MaxHoldSeconds}, you sent {requested}.");
                }

                hold = requested;
            }

            parsedData = hold;
            return ExecutionResult.Success($"Grabbing the {target.Label} in front of you.");
        }

        protected override void Execute(float? parsedData)
        {
            float hold = parsedData ?? DefaultHoldSeconds;
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.Grab(hold));
        }
    }
}
