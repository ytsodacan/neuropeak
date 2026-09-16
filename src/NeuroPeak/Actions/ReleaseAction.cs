using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class ReleaseAction : NeuroAction
    {
        public const string ActionName = "release";

        public override string Name => ActionName;

        protected override string Description =>
            "Let go of whatever you are holding onto. If you are up a wall you will fall, so only do this when it is safe.";

        protected override JsonSchema? Schema => null;

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            PeakIntentDriver? driver = PeakIntentDriver.Instance;
            bool holdingSomething = state.ClimbingAnything || state.HoldingClimbHandle || (driver != null && driver.GrabActive);
            if (!holdingSomething)
            {
                return ExecutionResult.Failure("You are not holding onto anything right now.");
            }

            return ExecutionResult.Success();
        }

        protected override void Execute()
        {
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.Release());
        }
    }
}
