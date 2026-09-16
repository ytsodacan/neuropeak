using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class DropItemAction : NeuroAction
    {
        public const string ActionName = "drop_item";

        public override string Name => ActionName;

        protected override string Description =>
            "Throw away the item you are holding. It lands on the ground and you can pick it up again.";

        protected override JsonSchema? Schema => null;

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (!state.HoldingItem)
            {
                return ExecutionResult.Failure("You are not holding anything to drop.");
            }

            return ExecutionResult.Success($"Dropping the {state.HeldItemName}.");
        }

        protected override void Execute()
        {
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.DropItem());
        }
    }
}
