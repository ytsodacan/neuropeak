using NeuroPeak.Core;
using NeuroSdk.Websocket;

namespace NeuroPeak.Actions
{
    public static class PeakActionGuards
    {
        public static ExecutionResult? Playable(PeakPlayerState state)
        {
            NeuroPeakConfig? settings = NeuroPeakPlugin.Settings;
            if (settings != null && !settings.MovementEnabled.Value)
            {
                return ExecutionResult.Failure("Movement control is disabled in the mod configuration.");
            }

            if (!state.InGame)
            {
                return ExecutionResult.Failure("You are not in the game right now, so there is nothing to control.");
            }

            if (state.Dead)
            {
                return ExecutionResult.Failure("You are dead and cannot move until someone revives you.");
            }

            if (state.FullyPassedOut)
            {
                return ExecutionResult.Failure("You are fully passed out and cannot move. A teammate has to help you up.");
            }

            if (PeakIntentDriver.Instance == null)
            {
                return ExecutionResult.Failure("The movement driver is not running yet, try again in a moment.");
            }

            return null;
        }

        public static void Dispatch(System.Action command) => MainThreadCommandQueue.Enqueue(command);
    }
}
