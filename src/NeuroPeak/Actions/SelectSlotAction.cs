using System.Collections.Generic;
using System.Linq;
using NeuroPeak.Core;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Websocket;
using UnityEngine;

namespace NeuroPeak.Actions
{
    public sealed class SelectSlotAction : NeuroActionS<int>
    {
        public const string ActionName = "select_slot";
        public const int StowSlot = 0;

        public override string Name => ActionName;

        protected override string Description =>
            "Take an item out of one of your bag slots, or pass 0 to put away whatever you are holding. You need empty hands to climb.";

        protected override JsonSchema? Schema => QJS.WrapObject(new Dictionary<string, JsonSchema>
        {
            ["slot"] = new JsonSchema
            {
                Type = JsonSchemaType.Integer,
                Minimum = 0,
                Maximum = 8
            }
        });

        protected override ExecutionResult Validate(ActionJData actionData, out int? parsedData)
        {
            parsedData = null;

            PeakPlayerState state = PeakStateTracker.Current;
            ExecutionResult? blocked = PeakActionGuards.Playable(state);
            if (blocked != null) return blocked;

            if (state.ClimbingAnything)
            {
                return ExecutionResult.Failure("You cannot rummage through your bag while hanging off the wall.");
            }

            if (!ActionDataReader.TryReadFloat(actionData, "slot", out float rawSlot))
            {
                return ExecutionResult.Failure($"Missing required parameter 'slot'. {DescribeInventory(state)}");
            }

            int slot = Mathf.RoundToInt(rawSlot);
            if (slot == StowSlot)
            {
                parsedData = StowSlot;
                return ExecutionResult.Success("Putting your item away.");
            }

            InventorySlot[] usable = state.Inventory.Where(s => !s.IsBackpack).ToArray();
            InventorySlot match = usable.FirstOrDefault(s => s.Number == slot);
            if (match.Number != slot)
            {
                return ExecutionResult.Failure($"You have no slot {slot}. {DescribeInventory(state)}");
            }

            if (match.Empty)
            {
                return ExecutionResult.Failure($"Slot {slot} is empty. {DescribeInventory(state)}");
            }

            parsedData = slot;
            return ExecutionResult.Success($"Taking out the {match.ItemName}.");
        }

        private static string DescribeInventory(PeakPlayerState state)
        {
            InventorySlot[] filled = state.Inventory.Where(s => !s.Empty && !s.IsBackpack).ToArray();
            if (filled.Length == 0) return "Your bag is empty.";

            string list = string.Join(", ", filled.Select(s => $"{s.Number} is a {s.ItemName}"));
            return $"In your bag: {list}. Pass 0 to put your hands away.";
        }

        protected override void Execute(int? parsedData)
        {
            if (parsedData == null) return;

            int slot = parsedData.Value;
            PeakActionGuards.Dispatch(() => PeakEquipment.EquipSlot(slot));
        }
    }
}
