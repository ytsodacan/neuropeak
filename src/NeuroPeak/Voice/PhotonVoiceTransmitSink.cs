using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

namespace NeuroPeak.Voice
{
    public sealed class PhotonVoiceTransmitSink : IPeakVoiceTransmitSink, IAudioReader<float>
    {
        public const int WireSampleRate = 48000;

        private const int BufferSamples = WireSampleRate * 2;

        private readonly NeuroVoiceBuffer _buffer = new NeuroVoiceBuffer(BufferSamples);

        private Recorder? _recorder;
        private bool _transmitting;

        public static bool TryInstall()
        {
            PhotonVoiceTransmitSink sink = new PhotonVoiceTransmitSink();
            PeakVoiceTransmitRegistry.Register(sink);
            return true;
        }

        public string Name => "photon-voice";

        public bool Available => _recorder != null;

        public int Channels => 1;

        public int SamplingRate => WireSampleRate;

        public string? Error => null;

        public void Attach(Character localCharacter)
        {
            if (localCharacter == null) return;

            Recorder recorder = localCharacter.GetComponentInChildren<Recorder>(true);
            if (recorder == null)
            {
                NeuroPeakPlugin.Log?.LogWarning("No Photon Voice Recorder was found on the local character, Neuro's voice cannot be transmitted.");
                return;
            }

            _recorder = recorder;
            _recorder.SourceType = Recorder.InputSourceType.Factory;
            _recorder.InputFactory = () => this;
            _recorder.TransmitEnabled = false;
            _recorder.RestartRecording();

            NeuroPeakPlugin.Log?.LogInfo("Neuro's voice is now routed into PEAK's Photon Voice recorder.");
        }

        public void Detach()
        {
            _buffer.Clear();

            if (_recorder == null) return;
            _recorder.TransmitEnabled = false;
            _recorder = null;
        }

        public void SetTransmitting(bool transmitting)
        {
            _transmitting = transmitting;
            if (_recorder != null) _recorder.TransmitEnabled = transmitting;
        }

        public void SubmitNeuroAudio(float[] monoPcm48k) => _buffer.Write(monoPcm48k);

        public void Flush() => _buffer.Clear();

        public bool Read(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return false;
            if (!_transmitting && _buffer.Available == 0) return false;
            if (_buffer.Available < buffer.Length) return false;

            int read = _buffer.Read(buffer, 0, buffer.Length);
            if (read < buffer.Length)
            {
                for (int i = read; i < buffer.Length; i++) buffer[i] = 0f;
            }

            return true;
        }

        public void Dispose() => _buffer.Clear();
    }
}
