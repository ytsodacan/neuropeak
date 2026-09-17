using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class OpenBackpackAction : NeuroAction
    {
        public const string ActionName = "open_backpack";

        public override string Name => ActionName;

        protected override string Description =>
            "Open the backpack you are wearing, or the one you are looking at, to get at what is inside. A backpack holds more than your own slots do.";

        protected override JsonSchema? Schema => null;

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (state.ClimbingAnything)
            {
                return ExecutionResult.Failure("You cannot go through a backpack while hanging off a wall.");
            }

            if (!state.WearingBackpack && !state.LookingAtBackpack)
            {
                return ExecutionResult.Failure(
                    "You are not wearing a backpack and you are not looking at one. Find one and 'interact' to pick it up first.");
            }

            return ExecutionResult.Success();
        }

        protected override void Execute()
        {
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.OpenBackpack());
        }
    }
}
