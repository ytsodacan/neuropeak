using System.Collections.Generic;
using UnityEngine;

namespace NeuroPeak.Core
{
    public sealed class PeakPlayerState
    {
        public static readonly PeakPlayerState NotInGame = new PeakPlayerState();

        public bool InGame;
        public bool InRun;
        public bool Grounded;
        public float SinceGrounded;
        public float SinceJump;
        public int JumpsRemaining;
        public bool Climbing;
        public bool RopeClimbing;
        public bool VineClimbing;
        public bool HoldingClimbHandle;
        public bool Crouching;
        public bool Sprinting;
        public float CurrentStamina;
        public float ExtraStamina;
        public float TotalStamina;
        public float MaxStamina;
        public bool OutOfStamina;
        public float OutOfStaminaFor;
        public bool FullyConscious;
        public bool PassedOut;
        public bool FullyPassedOut;
        public bool Dead;
        public bool InFog;
        public bool HoldingItem;
        public bool UsingItem;
        public string HeldItemName = string.Empty;
        public float Injury;
        public float StatusSum;
        public List<AfflictionReading> Afflictions = new List<AfflictionReading>();
        public float AltitudeMeters;
        public float FallSeconds;
        public Vector3 Position;
        public Vector3 HeadPosition;
        public Vector3 LookDirection = Vector3.forward;
        public Vector3 LookFlat = Vector3.forward;
        public Vector3 LookRight = Vector3.right;
        public Vector3 Velocity;
        public string SegmentName = string.Empty;
        public List<InventorySlot> Inventory = new List<InventorySlot>();
        public int SelectedSlotNumber = -1;
        public bool LookingAtSomething;
        public string LookingAtName = string.Empty;
        public string LookingAtPrompt = string.Empty;
        public bool CanInteract;
        public bool WearingBackpack;
        public bool LookingAtBackpack;
        public bool Reaching;
        public string ReachingTeammate = string.Empty;
        public float ReachingTeammateDistance;

        public bool HasFreeHands => !HoldingItem;

        public bool ClimbingAnything => Climbing || RopeClimbing || VineClimbing;

        public float StaminaFraction => MaxStamina > 0.0001f ? Mathf.Clamp01(TotalStamina / MaxStamina) : 0f;
    }
}
