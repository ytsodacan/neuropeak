using System;
using Zorro.Core;

namespace NeuroPeak.Core
{
    public static class PeakEquipment
    {
        public static void EquipSlot(int slotNumber)
        {
            Character local = Character.localCharacter;
            if (local == null) return;

            CharacterItems items = local.refs.items;
            if (items == null) return;

            try
            {
                if (slotNumber <= 0)
                {
                    items.EquipSlot(Optionable<byte>.None);
                    return;
                }

                items.EquipSlot(Optionable<byte>.Some((byte)(slotNumber - 1)));
            }
            catch (Exception e)
            {
                NeuroPeakPlugin.Log?.LogWarning($"Could not equip slot {slotNumber}: {e.Message}");
            }
        }
    }
}
