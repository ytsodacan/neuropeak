using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public sealed class PingAction : NeuroAction
    {
        public const string ActionName = "ping";

        public override string Name => ActionName;

        protected override string Description =>
            "Mark whatever you are looking at so your teammates see it. Use it to point out a route, an item, or danger.";

        protected override JsonSchema? Schema => null;

        protected override ExecutionResult Validate(ActionJData actionData)
        {
            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (!state.FullyConscious)
            {
                return ExecutionResult.Failure("You are barely conscious and cannot point at anything.");
            }

            return ExecutionResult.Success();
        }

        protected override void Execute()
        {
            PeakActionGuards.Dispatch(() => PeakIntentDriver.Instance?.Ping());
        }
    }
}
