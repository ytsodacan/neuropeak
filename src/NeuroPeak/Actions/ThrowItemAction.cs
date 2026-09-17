using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class ThrowItemAction : NeuroActionS<float>
    {
        public const string ActionName = "throw_item";
        public const float MinCharge = 0.2f;
        public const float MaxCharge = 3f;
        public const float DefaultCharge = 1f;

        public override string Name => ActionName;

        protected override string Description =>
            "Throw the item you are holding at whatever you are looking at. Charge it longer to throw further. Useful for passing something to a teammate who is out of reach.";

        protected override JsonSchema? Schema
        {
            get
            {
                JsonSchema schema = QJS.WrapObject(new Dictionary<string, JsonSchema>
                {
                    ["charge_seconds"] = new JsonSchema
                    {
                        Type = JsonSchemaType.Float,
                        Minimum = MinCharge,
                        Maximum = MaxCharge
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

            if (!state.HoldingItem)
            {
                return ExecutionResult.Failure("You are not holding anything to throw.");
            }

            float charge = DefaultCharge;
            if (ActionDataReader.TryReadFloat(actionData, "charge_seconds", out float requested))
            {
                if (requested < MinCharge || requested > MaxCharge)
                {
                    return ExecutionResult.Failure($"'charge_seconds' must be between {MinCharge} and {MaxCharge}, you sent {requested}.");
                }

                charge = requested;
            }

            parsedData = charge;
            return ExecutionResult.Success($"Throwing the {state.HeldItemName}.");
        }

        protected override void Execute(float? parsedData)
        {
            float charge = parsedData ?? DefaultCharge;
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.ThrowItem(charge));
        }
    }
}
