namespace NeuroPeak.Voice
{
    public sealed class BufferedVoiceTransmitSink : IPeakVoiceTransmitSink
    {
        private const int BufferSamples = 48000 * 2;

        private bool _warnedOnce;

        public NeuroVoiceBuffer Buffer { get; } = new NeuroVoiceBuffer(BufferSamples);

        public bool Transmitting { get; private set; }

        public string Name => "buffered";

        public bool Available => true;

        public void Attach(Character localCharacter)
        {
        }

        public void Detach() => Flush();

        public void SetTransmitting(bool transmitting) => Transmitting = transmitting;

        public void SubmitNeuroAudio(float[] monoPcm48k)
        {
            Buffer.Write(monoPcm48k);

            if (_warnedOnce) return;
            _warnedOnce = true;
            NeuroPeakPlugin.Log?.LogWarning(
                "Neuro's voice is being buffered but nothing is transmitting it into PEAK's voice chat. " +
                "Add PhotonVoice.API.dll and PhotonVoice.dll to lib/ and rebuild, or register your own IPeakVoiceTransmitSink through PeakVoiceTransmitRegistry.");
        }

        public void Flush() => Buffer.Clear();
    }
}
