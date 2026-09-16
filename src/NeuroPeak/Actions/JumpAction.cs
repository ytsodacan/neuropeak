using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class JumpAction : NeuroAction
    {
        public const string ActionName = "jump";

        public override string Name => ActionName;

        protected override string Description =>
            "Jump. Only works with both feet on the ground, and it costs stamina.";

        protected override JsonSchema? Schema => null;

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (state.ClimbingAnything || state.HoldingClimbHandle)
            {
                return ExecutionResult.Failure(
                    "You are holding onto the wall, so you cannot jump. Use 'release' to let go first, or 'sprint' to hop up while climbing.");
            }

            if (!state.Grounded && state.SinceGrounded > 0.2f)
            {
                return ExecutionResult.Failure(
                    $"You are in mid air ({RelativePosition.FormatDistance(state.FallSeconds * 9.8f)} into a fall) and cannot jump again.");
            }

            if (state.SinceJump < 0.3f)
            {
                return ExecutionResult.Failure("You just jumped, wait a moment before jumping again.");
            }

            if (state.JumpsRemaining <= 0)
            {
                return ExecutionResult.Failure("You have no jumps left until you land properly.");
            }

            if (state.OutOfStamina)
            {
                return ExecutionResult.Failure("You are out of stamina. Rest on solid ground until it comes back.");
            }

            return ExecutionResult.Success();
        }

        protected override void Execute()
        {
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.Jump());
        }
    }
}
