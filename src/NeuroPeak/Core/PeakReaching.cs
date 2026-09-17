using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroPeak.Core
{
    public static class PeakReaching
    {
        public const float FallbackReachDistance = 4f;

        public static bool AnyoneWithinReach(PeakPlayerState state)
        {
            Character local = Character.localCharacter;
            if (local == null) return false;

            try
            {
                List<Character> all = Character.AllCharacters;
                if (all == null) return false;

                for (int i = 0; i < all.Count; i++)
                {
                    Character other = all[i];
                    if (other == null || other == local) continue;

                    if (CharacterGrabbing.TargetCanBeHelped(local, other)) return true;

                    float distance = Vector3.Distance(other.transform.position, state.Position);
                    if (distance <= FallbackReachDistance) return true;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }
    }
}
