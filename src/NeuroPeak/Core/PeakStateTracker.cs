using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace NeuroPeak.Core
{
    public sealed class PeakStateTracker : MonoBehaviour
    {
        private const float MapHandlerLookupInterval = 2f;

        private static PeakPlayerState _current = PeakPlayerState.NotInGame;
        private static MapHandler? _mapHandler;
        private static float _lastMapHandlerLookup = -100f;

        public static PeakPlayerState Current => Volatile.Read(ref _current);

        public static Character? LocalCharacter
        {
            get
            {
                Character local = Character.localCharacter;
                return local == null ? null : local;
            }
        }

        private void Update()
        {
            Volatile.Write(ref _current, Capture());
        }

        private static PeakPlayerState Capture()
        {
            Character? character = LocalCharacter;
            if (character == null) return PeakPlayerState.NotInGame;

            CharacterData data = character.data;
            if (data == null) return PeakPlayerState.NotInGame;

            PeakPlayerState state = new PeakPlayerState
            {
                InGame = true,
                Grounded = data.isGrounded,
                SinceGrounded = data.sinceGrounded,
                SinceJump = data.sinceJump,
                JumpsRemaining = data.jumpsRemaining,
                Climbing = data.isClimbing,
                RopeClimbing = data.isRopeClimbing,
                VineClimbing = data.isVineClimbing,
                HoldingClimbHandle = data.currentClimbHandle != null,
                Crouching = data.isCrouching,
                Sprinting = data.isSprinting,
                CurrentStamina = data.currentStamina,
                ExtraStamina = data.extraStamina,
                TotalStamina = data.TotalStamina,
                MaxStamina = character.GetMaxStamina(),
                OutOfStaminaFor = data.outOfStaminaFor,
                FullyConscious = data.fullyConscious,
                PassedOut = data.passedOut,
                FullyPassedOut = data.fullyPassedOut,
                Dead = data.dead,
                InFog = data.isInFog,
                FallSeconds = data.fallSeconds,
                Position = character.transform.position,
                LookDirection = data.lookDirection,
                LookFlat = data.lookDirection_Flat,
                LookRight = data.lookDirection_Right,
                Velocity = data.avarageVelocity
            };

            state.OutOfStamina = state.CurrentStamina < 0.005f && state.ExtraStamina < 0.001f;
            state.HeadPosition = HeadPositionOf(character, state.Position);

            Item heldItem = data.currentItem;
            if (heldItem != null)
            {
                state.HoldingItem = true;
                state.HeldItemName = PeakItemKnowledge.DescribeWithName(heldItem, CleanName(heldItem.name));
                state.UsingItem = heldItem.isUsingPrimary || heldItem.isUsingSecondary;
            }

            CharacterAfflictions afflictions = character.refs.afflictions;
            if (afflictions != null)
            {
                state.Injury = afflictions.GetCurrentStatus(CharacterAfflictions.STATUSTYPE.Injury);
                state.StatusSum = afflictions.statusSum;
                state.Afflictions = PeakAfflictions.Read(character);
            }

            CharacterStats stats = character.refs.stats;
            if (stats != null) state.AltitudeMeters = stats.heightInMeters;

            state.SegmentName = CurrentSegmentName();
            state.InRun = DetectRun();
            state.Reaching = data.isReaching;
            CaptureReachingTeammate(character, state);
            CaptureInventory(character, state);
            CaptureLookTarget(state);

            return state;
        }

        private static void CaptureInventory(Character character, PeakPlayerState state)
        {
            try
            {
                Player player = character.player;
                if (player == null || player.itemSlots == null) return;

                CharacterItems items = character.refs.items;
                int selected = items != null && items.currentSelectedSlot.IsSome ? items.currentSelectedSlot.Value : -1;
                state.SelectedSlotNumber = selected >= 0 ? selected + 1 : -1;

                for (int i = 0; i < player.itemSlots.Length; i++)
                {
                    ItemSlot slot = player.itemSlots[i];
                    if (slot == null) continue;

                    bool empty = slot.IsEmpty();
                    state.Inventory.Add(new InventorySlot
                    {
                        Number = i + 1,
                        Empty = empty,
                        ItemName = empty ? string.Empty : DescribeSlotItem(slot),
                        Selected = i == selected,
                        IsBackpack = false
                    });
                }

                BackpackSlot backpack = player.GetBackpackSlot();
                if (backpack != null && !backpack.IsEmpty())
                {
                    state.WearingBackpack = true;
                    state.Inventory.Add(new InventorySlot
                    {
                        Number = 0,
                        Empty = false,
                        ItemName = "backpack",
                        Selected = false,
                        IsBackpack = true
                    });
                }
            }
            catch (Exception)
            {
            }
        }

        private static string DescribeSlotItem(ItemSlot slot)
        {
            try
            {
                return PeakItemKnowledge.DescribeWithName(slot.prefab, CleanName(slot.GetPrefabName()));
            }
            catch (Exception)
            {
                return "something";
            }
        }

        private static void CaptureReachingTeammate(Character character, PeakPlayerState state)
        {
            try
            {
                List<Character> all = Character.AllCharacters;
                if (all == null) return;

                float nearest = float.MaxValue;

                for (int i = 0; i < all.Count; i++)
                {
                    Character other = all[i];
                    if (other == null || other == character) continue;

                    CharacterData otherData = other.data;
                    if (otherData == null || !otherData.isReaching) continue;

                    float distance = Vector3.Distance(other.transform.position, state.Position);
                    if (distance >= nearest) continue;

                    nearest = distance;
                    state.ReachingTeammate = other.characterName;
                    state.ReachingTeammateDistance = distance;
                }
            }
            catch (Exception)
            {
            }
        }

        private static void CaptureLookTarget(PeakPlayerState state)
        {
            try
            {
                Interaction interaction = Interaction.instance;
                if (interaction == null) return;

                IInteractible hovered = interaction.currentHovered;
                if (hovered == null) return;

                state.CanInteract = true;
                state.LookingAtSomething = true;
                state.LookingAtName = hovered.GetName() ?? string.Empty;
                state.LookingAtPrompt = hovered.GetInteractionText() ?? string.Empty;
                state.LookingAtBackpack = state.LookingAtName.IndexOf("backpack", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
            }
        }

        public static Vector3 HeadPositionOf(Character character, Vector3 fallback)
        {
            Bodypart head = character.refs.head;
            return head != null ? head.transform.position : fallback + Vector3.up;
        }

        public static Vector3 EyePosition(PeakPlayerState state)
        {
            MainCamera camera = MainCamera.instance;
            return camera != null ? camera.transform.position : state.HeadPosition;
        }

        private static bool DetectRun()
        {
            if (CachedMapHandler() != null) return true;

            try
            {
                RunManager runManager = RunManager.Instance;
                return runManager != null && runManager.TimeSinceRunStarted > 0f;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string CurrentSegmentName()
        {
            MapHandler? handler = CachedMapHandler();
            if (handler == null) return string.Empty;

            try
            {
                return handler.GetCurrentSegment().ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static MapHandler? CachedMapHandler()
        {
            if (_mapHandler != null) return _mapHandler;
            if (Time.unscaledTime - _lastMapHandlerLookup < MapHandlerLookupInterval) return null;

            _lastMapHandlerLookup = Time.unscaledTime;
            MapHandler found = UnityEngine.Object.FindObjectOfType<MapHandler>();
            _mapHandler = found == null ? null : found;
            return _mapHandler;
        }

        private static string CleanName(string rawName)
        {
            int cloneIndex = rawName.IndexOf("(Clone)", StringComparison.Ordinal);
            return cloneIndex >= 0 ? rawName.Substring(0, cloneIndex).Trim() : rawName;
        }
    }
}
