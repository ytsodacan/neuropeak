using NeuroPeak.Core;
using NeuroPeak.Perception;

namespace NeuroPeak.Patches
{
    public static class CheckpointPatch
    {
        public static void Postfix(Campfire __instance, bool updateSegment)
        {
            if (__instance == null) return;
            if (!updateSegment) return;

            string nextArea = __instance.advanceToSegment.ToString();
            UrgentContext.Report($"{UrgentContext.CheckpointKey}:{nextArea}",
                $"The campfire is lit. This is a checkpoint, so if the team wipes you all start again from here, and the way on towards the {nextArea} is open.");
        }
    }

    public static class SegmentAdvancedPatch
    {
        public static void Postfix(Segment s)
        {
            NeuroContext.SendAmbient($"## New area\nYou have climbed into the {s}. The ground below is further away than it was.");
        }
    }
}
