using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;

namespace NeuroPeak.Core
{
    public static class NeuroContext
    {
        public static bool Ready => WebsocketConnection.Instance != null;

        public static void SendAmbient(string message) => Send(message, true);

        public static void SendUrgent(string message) => Send(message, false);

        public static void Send(string message, bool silent)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!Ready)
            {
                NeuroPeakPlugin.Log?.LogDebug($"Context dropped before the SDK was up: {message}");
                return;
            }

            Context.Send(message, silent);
        }
    }
}
