namespace NeuroPeak.Voice
{
    public static class PeakVoiceTransmitRegistry
    {
        private static IPeakVoiceTransmitSink _current = new BufferedVoiceTransmitSink();

        public static IPeakVoiceTransmitSink Current => _current;

        public static void Register(IPeakVoiceTransmitSink sink)
        {
            if (sink == null) return;

            _current.Detach();
            _current = sink;
            NeuroPeakPlugin.Log?.LogInfo($"Voice transmit sink set to \"{sink.Name}\"");
        }

        public static void ResetToBuffered() => Register(new BufferedVoiceTransmitSink());
    }
}
