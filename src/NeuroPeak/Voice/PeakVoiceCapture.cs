using System.Collections.Concurrent;
using UnityEngine;

namespace NeuroPeak.Voice
{
    public sealed class CapturedVoiceChunk
    {
        public float[] Samples = System.Array.Empty<float>();
        public int Channels = 1;
    }

    public sealed class PeakVoiceCapture : MonoBehaviour
    {
        private const int MaxQueuedChunks = 24;

        private readonly ConcurrentQueue<CapturedVoiceChunk> _chunks = new ConcurrentQueue<CapturedVoiceChunk>();

        private float _activityThreshold = 0.0025f;
        private int _queued;

        public int SpeakerId { get; private set; } = -1;

        public string SpeakerName { get; private set; } = string.Empty;

        public int SampleRate { get; private set; } = 48000;

        public void Configure(int speakerId, string speakerName, int sampleRate, float activityThreshold)
        {
            SpeakerId = speakerId;
            SpeakerName = speakerName;
            SampleRate = sampleRate;
            _activityThreshold = activityThreshold;
        }

        public bool TryDequeue(out CapturedVoiceChunk chunk)
        {
            bool taken = _chunks.TryDequeue(out chunk);
            if (taken) System.Threading.Interlocked.Decrement(ref _queued);
            return taken;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (SpeakerId < 0) return;
            if (_queued >= MaxQueuedChunks) return;
            if (!CarriesVoice(data)) return;

            float[] copy = new float[data.Length];
            System.Array.Copy(data, copy, data.Length);

            _chunks.Enqueue(new CapturedVoiceChunk { Samples = copy, Channels = channels });
            System.Threading.Interlocked.Increment(ref _queued);
        }

        private bool CarriesVoice(float[] data)
        {
            float sum = 0f;
            for (int i = 0; i < data.Length; i++) sum += data[i] < 0f ? -data[i] : data[i];
            return data.Length > 0 && sum / data.Length > _activityThreshold;
        }
    }
}
