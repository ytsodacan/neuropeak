using System.Collections.Generic;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public struct UseItemCommand
    {
        public bool Secondary;
        public float HoldSeconds;
    }

    public sealed class UseItemAction : NeuroActionS<UseItemCommand>
    {
        public const string ActionName = "use_item";
        public const string PrimaryMode = "primary";
        public const string SecondaryMode = "secondary";
        public const float MinHold = 0.1f;
        public const float MaxHold = 15f;
        public const float DefaultHold = 1.5f;

        private static readonly string[] Modes = { PrimaryMode, SecondaryMode };

        public override string Name => ActionName;

        protected override string Description =>
            "Use the item you are holding. Eating food, drinking, using a medkit and firing a flare are all the primary use. Some items do something different on secondary.";

        protected override JsonSchema? Schema
        {
            get
            {
                JsonSchema schema = QJS.WrapObject(new Dictionary<string, JsonSchema>
                {
                    ["mode"] = QJS.Enum(Modes),
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

        protected override ExecutionResult Validate(ActionJData actionData, out UseItemCommand? parsedData)
        {
            parsedData = null;

            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (!state.HoldingItem)
            {
                return ExecutionResult.Failure(
                    "You are not holding anything. Use 'select_slot' to take something out of your bag first.");
            }

            if (state.ClimbingAnything)
            {
                return ExecutionResult.Failure("You cannot use an item while you are hanging off the wall. Get onto solid ground first.");
            }

            string? rawMode = ActionDataReader.ReadString(actionData, "mode");
            bool secondary = false;
            if (!string.IsNullOrEmpty(rawMode))
            {
                string mode = rawMode!.Trim().ToLowerInvariant();
                if (mode != PrimaryMode && mode != SecondaryMode)
                {
                    return ExecutionResult.Failure($"'mode' must be {PrimaryMode} or {SecondaryMode}, you sent '{rawMode}'.");
                }

                secondary = mode == SecondaryMode;
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

            parsedData = new UseItemCommand { Secondary = secondary, HoldSeconds = hold };
            return ExecutionResult.Success($"Using the {state.HeldItemName}.");
        }

        protected override void Execute(UseItemCommand? parsedData)
        {
            if (parsedData == null) return;

            UseItemCommand command = parsedData.Value;
            PeakActionGuards.Dispatch(() =>
            {
                PeakIntentDriver? driver = PeakIntentDriver.Instance;
                if (driver == null) return;
                if (command.Secondary) driver.UseSecondary(command.HoldSeconds);
                else driver.UsePrimary(command.HoldSeconds);
            });
        }
    }
}
