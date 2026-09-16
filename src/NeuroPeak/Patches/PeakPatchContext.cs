using UnityEngine;

namespace NeuroPeak.Patches
{
    public static class PeakPatchContext
    {
        public static Character? OwnerOf(Component component)
        {
            if (component == null) return null;

            Character owner = component.GetComponentInParent<Character>();
            return owner == null ? null : owner;
        }
    }
}
