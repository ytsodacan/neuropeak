using System.Collections.Generic;
using System.Text;
using NeuroPeak.Core;
using UnityEngine;

namespace NeuroPeak.Perception
{
    public static class SceneReportBuilder
    {
        private const int MaxSurfacesReported = 3;
        private const int MaxThingsReported = 5;
        private const int MaxTeammatesReported = 4;
        private const float MinSurfaceSeparation = 2.5f;

        public static SceneReport Build(PeakPlayerState state)
        {
            SceneReport report = new SceneReport();
            if (!state.InGame) return report;

            float dropBelow = PeakSurfaceProbe.GroundDistanceBelow(state.Position + Vector3.up * 0.2f);
            List<SurfaceHit> surfaces = FindClimbableSurfaces(state);
            List<NearbyThing> things = FindNearbyThings(state);
            AscentReading ascent = PeakTerrainScout.FindWayUp(state);
            List<string> teammates = DescribeTeammates(state);

            StringBuilder text = new StringBuilder();
            text.Append("## Around you\n");
            text.Append(Posture(state, dropBelow)).Append('\n');
            text.Append("World coordinates ").Append(RelativePosition.Coordinates(state.Position))
                .Append(", facing ").Append(RelativePosition.Compass(state.LookFlat)).Append(".\n");
            text.Append(Condition(state)).Append('\n');

            if (state.LookingAtSomething)
            {
                text.Append("You are looking at ").Append(state.LookingAtName);
                if (!string.IsNullOrEmpty(state.LookingAtPrompt))
                {
                    text.Append(" — you could ").Append(state.LookingAtPrompt.ToLowerInvariant());
                }

                text.Append(". Use `interact` for that.\n");
            }

            if (surfaces.Count > 0)
            {
                text.Append("Climbable:\n");
                foreach (SurfaceHit surface in surfaces)
                {
                    text.Append("- ").Append(surface.Label).Append(' ')
                        .Append(surface.InReach
                            ? "right in front of you, close enough to grab"
                            : RelativePosition.DescribeWithCoordinates(state, surface.Point))
                        .Append('\n');
                }
            }
            else
            {
                text.Append("Nothing you could climb is in reach. Try looking around.\n");
            }

            if (things.Count > 0)
            {
                text.Append("Nearby:\n");
                foreach (NearbyThing thing in things)
                {
                    text.Append("- ").Append(thing.Description).Append(' ')
                        .Append(RelativePosition.DescribeWithCoordinates(state, thing.Position)).Append('\n');
                }
            }

            text.Append(WayUpLine(state, ascent)).Append('\n');

            string hazards = HazardLine(state, dropBelow);
            if (hazards.Length > 0) text.Append(hazards).Append('\n');

            text.Append(InventoryLine(state)).Append('\n');

            if (teammates.Count > 0)
            {
                text.Append("Your team:\n");
                foreach (string teammate in teammates) text.Append("- ").Append(teammate).Append('\n');
            }

            report.Description = text.ToString().TrimEnd();
            report.Digest = BuildDigest(state, dropBelow, surfaces, things, teammates, ascent);
            report.Meaningful = true;
            return report;
        }

        private static string Posture(PeakPlayerState state, float dropBelow)
        {
            string altitude = state.AltitudeMeters > 0.5f
                ? $" at about {Mathf.RoundToInt(state.AltitudeMeters)} m up"
                : string.Empty;

            string region = state.SegmentName.Length > 0 ? $" in the {Readable(state.SegmentName)}" : string.Empty;

            if (state.Climbing) return $"You are hanging off a rock face{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground below you.";
            if (state.RopeClimbing) return $"You are on a rope{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground below you.";
            if (state.VineClimbing) return $"You are on a vine{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground below you.";
            if (state.HoldingClimbHandle) return $"You are hanging from a handhold{altitude}{region}.";
            if (!state.Grounded && state.SinceGrounded > 0.4f) return $"You are in the air{altitude}{region}, {RelativePosition.FormatDistance(dropBelow)} above the ground.";
            return $"You are standing on solid ground{altitude}{region}.";
        }

        private static string WayUpLine(PeakPlayerState state, AscentReading ascent)
        {
            if (!ascent.Found)
            {
                return "The ground around you is flat, so the way on is not obvious from here. Look around for something to climb.";
            }

            string heading = RelativePosition.CompassOnly(ascent.Direction);
            string relative = RelativePosition.Bearing(state, ascent.Direction);
            int gain = Mathf.RoundToInt(ascent.HeightGain);

            return $"The mountain rises to the {heading}, which is {relative} from where you are facing — the ground is about {gain} m higher {Mathf.RoundToInt(ascent.SampleDistance)} m that way. Head there to keep climbing.";
        }

        private static string Condition(PeakPlayerState state)
        {
            int percent = Mathf.RoundToInt(state.StaminaFraction * 100f);

            string stamina = state.OutOfStamina
                ? "You have nothing left in the tank"
                : percent >= 85 ? $"You are fresh, {percent}% stamina"
                : percent >= 40 ? $"Stamina is down to {percent}%"
                : $"You are running low, only {percent}% stamina";

            if (state.StatusSum <= 0.05f) return stamina + ".";

            int ceiling = Mathf.RoundToInt(Mathf.Max(1f - state.StatusSum, 0f) * 100f);
            return $"{stamina}, and you are worn down enough that it will not refill past {ceiling}%.";
        }

        private static string InventoryLine(PeakPlayerState state)
        {
            System.Collections.Generic.List<string> carried = new System.Collections.Generic.List<string>();
            bool hasBackpack = false;

            foreach (InventorySlot slot in state.Inventory)
            {
                if (slot.IsBackpack) { hasBackpack = true; continue; }
                if (slot.Empty) continue;
                carried.Add($"{slot.Number}: {slot.ItemName}");
            }

            string held = state.HoldingItem
                ? $"You are holding the {state.HeldItemName}"
                : "Your hands are empty, so you can climb";

            if (carried.Count == 0)
            {
                return hasBackpack ? $"{held}. Your bag is empty but you are wearing a backpack." : $"{held}, and your bag is empty.";
            }

            string bag = $"In your bag: {string.Join(", ", carried.ToArray())}";
            return hasBackpack ? $"{held}. {bag}, plus a backpack." : $"{held}. {bag}.";
        }

        private static string HazardLine(PeakPlayerState state, float dropBelow)
        {
            List<string> hazards = new List<string>();
            float dangerous = NeuroPeakPlugin.Settings?.DangerousDropMeters.Value ?? 12f;

            if (dropBelow >= dangerous)
            {
                hazards.Add(dropBelow >= PeakSurfaceProbe.MaxGroundProbe
                    ? "the drop below you has no bottom in sight"
                    : $"a {RelativePosition.FormatDistance(dropBelow)} drop straight below you");
            }

            if (state.InFog) hazards.Add("fog thick enough that you cannot see far");
            if (state.Injury > 0.05f) hazards.Add($"you are hurt ({Mathf.RoundToInt(state.Injury * 100f)}% injured)");
            if (state.PassedOut) hazards.Add("you are on the edge of passing out");

            return hazards.Count == 0 ? string.Empty : $"Watch out: {string.Join(", and ", hazards.ToArray())}.";
        }

        private static List<SurfaceHit> FindClimbableSurfaces(PeakPlayerState state)
        {
            List<SurfaceHit> found = new List<SurfaceHit>();
            float radius = NeuroPeakPlugin.Settings?.NearbyScanRadius.Value ?? 7f;

            SurfaceHit inReach = PeakSurfaceProbe.ProbeGrabTarget(state);
            if (inReach.Found)
            {
                found.Add(inReach);
            }

            int count = PeakSurfaceProbe.OverlapNearby(state.HeadPosition, radius, out Collider[] buffer);
            for (int i = 0; i < count && found.Count < MaxSurfacesReported; i++)
            {
                Collider collider = buffer[i];
                if (collider == null) continue;
                if (PeakSurfaceProbe.BelongsToCharacter(collider)) continue;

                Vector3 closest = collider.ClosestPoint(state.HeadPosition);
                Vector3 toSurface = closest - state.HeadPosition;
                if (toSurface.sqrMagnitude < 0.01f) continue;

                if (!Physics.Raycast(state.HeadPosition, toSurface.normalized, out RaycastHit hit, toSurface.magnitude + 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!PeakSurfaceProbe.IsTerrainHit(hit.collider)) continue;
                if (!PeakSurfaceProbe.IsClimbableNormal(hit.normal)) continue;
                if (TooCloseToExisting(found, hit.point)) continue;

                found.Add(new SurfaceHit
                {
                    Found = true,
                    Point = hit.point,
                    Normal = hit.normal,
                    Distance = hit.distance,
                    Label = PeakSurfaceProbe.DescribeCollider(hit.collider)
                });
            }

            return found;
        }

        private static List<NearbyThing> FindNearbyThings(PeakPlayerState state)
        {
            List<NearbyThing> found = new List<NearbyThing>();
            List<Transform> seen = new List<Transform>();
            float radius = (NeuroPeakPlugin.Settings?.NearbyScanRadius.Value ?? 7f) * 2f;

            int count = PeakSurfaceProbe.OverlapNearby(state.HeadPosition, radius, out Collider[] buffer);
            for (int i = 0; i < count && found.Count < MaxThingsReported; i++)
            {
                Collider collider = buffer[i];
                if (collider == null) continue;
                if (PeakSurfaceProbe.BelongsToCharacter(collider)) continue;

                string description = string.Empty;
                Transform owner = collider.transform;

                Item item = collider.GetComponentInParent<Item>();
                if (item != null)
                {
                    owner = item.transform;
                    description = PeakItemKnowledge.DescribeWithName(item, owner.name);
                }
                else
                {
                    Campfire campfire = collider.GetComponentInParent<Campfire>();
                    if (campfire != null)
                    {
                        owner = campfire.transform;
                        description = campfire.Lit ? "a lit campfire, this stretch is checkpointed" : "an unlit campfire, light it to checkpoint here";
                    }
                    else
                    {
                        IInteractible interactible = collider.GetComponentInParent<IInteractible>();
                        if (interactible != null)
                        {
                            try
                            {
                                Transform t = interactible.GetTransform();
                                if (t != null) owner = t;
                                string name = interactible.GetName();
                                if (!string.IsNullOrEmpty(name)) description = name;
                            }
                            catch (System.Exception)
                            {
                            }
                        }
                    }
                }

                if (description.Length == 0) continue;
                if (seen.Contains(owner)) continue;

                seen.Add(owner);
                found.Add(new NearbyThing { Description = description, Position = owner.position });
            }

            return found;
        }

        private static bool TooCloseToExisting(List<SurfaceHit> found, Vector3 point)
        {
            foreach (SurfaceHit existing in found)
            {
                if ((existing.Point - point).sqrMagnitude < MinSurfaceSeparation * MinSurfaceSeparation) return true;
            }

            return false;
        }

        private static List<string> DescribeTeammates(PeakPlayerState state)
        {
            List<string> described = new List<string>();
            List<Character> all = Character.AllCharacters;
            if (all == null) return described;

            Character local = Character.localCharacter;

            for (int i = 0; i < all.Count && described.Count < MaxTeammatesReported; i++)
            {
                Character other = all[i];
                if (other == null || other == local) continue;

                CharacterData data = other.data;
                if (data == null) continue;

                string condition = data.dead ? "dead"
                    : data.fullyPassedOut ? "fully passed out"
                    : data.passedOut ? "passed out"
                    : data.isClimbing || data.isRopeClimbing || data.isVineClimbing ? "climbing"
                    : "on their feet";

                described.Add($"{other.characterName} is {condition}, {RelativePosition.Describe(state, other.transform.position)}");
            }

            return described;
        }

        private static string BuildDigest(PeakPlayerState state, float dropBelow, List<SurfaceHit> surfaces, List<NearbyThing> things, List<string> teammates, AscentReading ascent)
        {
            StringBuilder digest = new StringBuilder();
            digest.Append(state.SegmentName).Append('|')
                .Append(state.LookingAtName).Append('|')
                .Append(state.HeldItemName).Append('|')
                .Append(state.Inventory.Count).Append('|')
                .Append(Bucket(state.AltitudeMeters, 10f)).Append('|')
                .Append(Bucket(state.Position.x, 15f)).Append(',')
                .Append(Bucket(state.Position.z, 15f)).Append('|')
                .Append(Bucket(CompassDegrees(state.LookFlat), 45f)).Append('|')
                .Append(ascent.Found ? RelativePosition.CompassOnly(ascent.Direction) : "flat").Append('|')
                .Append(PostureKey(state)).Append('|')
                .Append(Bucket(state.StaminaFraction * 100f, 20f)).Append('|')
                .Append(Bucket(dropBelow, 10f)).Append('|')
                .Append(state.InFog ? 1 : 0).Append('|')
                .Append(Bucket(state.Injury * 100f, 25f)).Append('|')
                .Append(surfaces.Count).Append('|');

            foreach (SurfaceHit surface in surfaces) digest.Append(surface.Label).Append(',');
            digest.Append('|').Append(things.Count);
            foreach (NearbyThing thing in things) digest.Append(thing.Description).Append(',');
            digest.Append('|').Append(teammates.Count);
            foreach (string teammate in teammates) digest.Append(teammate).Append(',');

            return digest.ToString();
        }

        private static string PostureKey(PeakPlayerState state)
        {
            if (state.Climbing) return "climb";
            if (state.RopeClimbing) return "rope";
            if (state.VineClimbing) return "vine";
            if (state.HoldingClimbHandle) return "handle";
            if (!state.Grounded && state.SinceGrounded > 0.4f) return "air";
            return "ground";
        }

        private static int Bucket(float value, float size) => Mathf.RoundToInt(value / size);

        private static float CompassDegrees(Vector3 flatDirection)
        {
            Vector3 flat = new Vector3(flatDirection.x, 0f, flatDirection.z);
            if (flat.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Repeat(Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 360f);
        }

        private static string Readable(string segmentName)
        {
            switch (segmentName)
            {
                case "TheKiln": return "Kiln";
                case "Peak": return "final stretch to the Peak";
                default: return segmentName;
            }
        }
    }
}
