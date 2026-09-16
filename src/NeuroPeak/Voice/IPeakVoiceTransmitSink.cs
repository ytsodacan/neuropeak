namespace NeuroPeak.Voice
{
    public interface IPeakVoiceTransmitSink
    {
        string Name { get; }

        bool Available { get; }

        void Attach(Character localCharacter);

        void Detach();

        void SetTransmitting(bool transmitting);

        void SubmitNeuroAudio(float[] monoPcm48k);

        void Flush();
    }
}
