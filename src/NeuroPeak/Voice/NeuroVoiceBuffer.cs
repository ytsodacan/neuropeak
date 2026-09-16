using System;

namespace NeuroPeak.Voice
{
    public sealed class NeuroVoiceBuffer
    {
        private readonly object _gate = new object();
        private readonly float[] _samples;

        private int _readIndex;
        private int _available;

        public NeuroVoiceBuffer(int capacitySamples)
        {
            _samples = new float[Math.Max(capacitySamples, 960)];
        }

        public int Available
        {
            get
            {
                lock (_gate) return _available;
            }
        }

        public int Capacity => _samples.Length;

        public void Write(float[] incoming)
        {
            if (incoming == null || incoming.Length == 0) return;

            lock (_gate)
            {
                for (int i = 0; i < incoming.Length; i++)
                {
                    int writeIndex = (_readIndex + _available) % _samples.Length;
                    _samples[writeIndex] = incoming[i];

                    if (_available == _samples.Length)
                    {
                        _readIndex = (_readIndex + 1) % _samples.Length;
                    }
                    else
                    {
                        _available++;
                    }
                }
            }
        }

        public int Read(float[] destination, int offset, int count)
        {
            if (destination == null || count <= 0) return 0;

            lock (_gate)
            {
                int taken = Math.Min(count, _available);
                for (int i = 0; i < taken; i++)
                {
                    destination[offset + i] = _samples[_readIndex];
                    _readIndex = (_readIndex + 1) % _samples.Length;
                }

                _available -= taken;
                return taken;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _readIndex = 0;
                _available = 0;
            }
        }
    }
}
