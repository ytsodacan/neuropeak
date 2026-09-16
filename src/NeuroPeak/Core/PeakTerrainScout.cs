using UnityEngine;

namespace NeuroPeak.Core
{
    public struct AscentReading
    {
        public bool Found;
        public Vector3 Direction;
        public float HeightGain;
        public float SampleDistance;
    }

    public static class PeakTerrainScout
    {
        public const float SampleDistance = 14f;
        public const float ProbeHeight = 40f;
        public const float ProbeDepth = 120f;
        public const float MeaningfulGain = 1.5f;

        private static readonly float[] Headings = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        public static AscentReading FindWayUp(PeakPlayerState state)
        {
            AscentReading best = default(AscentReading);
            float bestGain = MeaningfulGain;

            float hereHeight = GroundHeightAt(state.Position);
            if (float.IsNaN(hereHeight)) return best;

            for (int i = 0; i < Headings.Length; i++)
            {
                float radians = Headings[i] * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
                Vector3 sample = state.Position + direction * SampleDistance;

                float height = GroundHeightAt(sample);
                if (float.IsNaN(height)) continue;

                float gain = height - hereHeight;
                if (gain <= bestGain) continue;

                bestGain = gain;
                best = new AscentReading
                {
                    Found = true,
                    Direction = direction,
                    HeightGain = gain,
                    SampleDistance = SampleDistance
                };
            }

            return best;
        }

        private static float GroundHeightAt(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * ProbeHeight;
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, ProbeDepth, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : float.NaN;
        }
    }
}
