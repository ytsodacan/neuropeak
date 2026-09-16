using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroPeak.Core
{
    public static class PeakItemKnowledge
    {
        private static readonly Dictionary<string, string> EffectByComponent = new Dictionary<string, string>
        {
            ["Action_ClearAllStatus"] = "clears everything wrong with you",
            ["Action_HealingGem"] = "heals injuries",
            ["Action_MoraleBoost"] = "gives the whole team stamina",
            ["Action_Antizooka"] = "cures afflictions",
            ["Action_Numb"] = "numbs pain",
            ["Action_InflictPoison"] = "poisonous, do not eat it",
            ["Action_RandomMushroomEffect"] = "a mushroom, the effect is a gamble",
            ["Action_Flare"] = "a signal flare",
            ["Action_Torch"] = "a torch, gives light",
            ["Action_LightLantern"] = "a lantern, gives light",
            ["Action_LightCandle"] = "a candle, gives light",
            ["Action_Balloon"] = "a balloon, slows your fall",
            ["Action_Parasol"] = "a parasol, slows your fall",
            ["Action_ApplyAntigrav"] = "makes you lighter for a while",
            ["Action_ApplySuperJump"] = "makes you jump much higher",
            ["Action_SuperJumpAmulet"] = "makes you jump much higher",
            ["Action_ApplyInfiniteStamina"] = "gives you endless stamina for a while",
            ["Action_Passport"] = "just your passport, no use on the mountain",
            ["Action_Guidebook"] = "a guidebook you can read",
            ["Action_WarpToBiome"] = "teleports you somewhere else",
            ["Action_WarpRandomly"] = "teleports you somewhere random",
            ["Action_WarpToRandomPlayer"] = "teleports you to a teammate",
            ["Action_CallScoutmaster"] = "calls the scoutmaster",
            ["Action_Die"] = "kills you, do not use it",
            ["Action_SacrificeFriend"] = "sacrifices a teammate",
            ["Action_ShowBinocularOverlay"] = "binoculars, lets you see far away",
            ["Action_TootMagicBugle"] = "a bugle, makes noise"
        };

        public static string Describe(Item? item)
        {
            if (item == null) return string.Empty;

            List<string> effects = new List<string>();

            try
            {
                Action_RestoreHunger hunger = item.GetComponentInChildren<Action_RestoreHunger>(true);
                if (hunger != null) effects.Add($"food, restores {Percent(hunger.restorationAmount)} hunger");

                Action_GiveExtraStamina extra = item.GetComponentInChildren<Action_GiveExtraStamina>(true);
                if (extra != null) effects.Add($"gives {Percent(extra.amount)} bonus stamina");

                foreach (Action_ModifyStatus status in item.GetComponentsInChildren<Action_ModifyStatus>(true))
                {
                    if (status == null) continue;
                    string verb = status.changeAmount < 0f ? "removes" : "adds";
                    effects.Add($"{verb} {Percent(Mathf.Abs(status.changeAmount))} {status.statusType}");
                }

                foreach (MonoBehaviour behaviour in item.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null) continue;
                    if (!EffectByComponent.TryGetValue(behaviour.GetType().Name, out string known)) continue;
                    if (!effects.Contains(known)) effects.Add(known);
                }
            }
            catch (Exception)
            {
            }

            return effects.Count == 0 ? string.Empty : string.Join(", ", effects.ToArray());
        }

        public static string DescribeWithName(Item? item, string fallbackName)
        {
            string name = fallbackName;
            try
            {
                if (item != null)
                {
                    string real = item.GetName();
                    if (!string.IsNullOrEmpty(real)) name = real;
                }
            }
            catch (Exception)
            {
            }

            string effect = Describe(item);
            return effect.Length == 0 ? name : $"{name} ({effect})";
        }

        private static string Percent(float fraction) => $"{Mathf.RoundToInt(Mathf.Abs(fraction) * 100f)}%";
    }
}
