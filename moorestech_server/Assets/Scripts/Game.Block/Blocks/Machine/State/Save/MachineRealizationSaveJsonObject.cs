using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.Block.Blocks.Machine.Module;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.Block.Blocks.Machine.State.Save
{
    // 待機中の確定結果を現在の効果で引き直さず保存する
    // Persist a waiting realization without recomputing current effects
    public sealed class MachineRealizationSaveJsonObject
    {
        [JsonProperty("recipeGuid")] public string RecipeGuid;
        [JsonProperty("inheritedLevel")] public int? InheritedLevel;
        [JsonProperty("outputs")] public List<ItemStackSaveJsonObject> Outputs;
        [JsonProperty("totalTicks")] public uint TotalTicks;
        [JsonProperty("powerMultiplier")] public float? PowerMultiplier;
        [JsonProperty("drawRecord")] public JObject DrawRecord;

        internal static MachineRealizationSaveJsonObject Capture(IdleMachineProcessState idle, JObject drawRecord)
        {
            if (!idle.TryGetPendingRealization(out var pending)) return null;
            return new MachineRealizationSaveJsonObject
            {
                RecipeGuid = pending.Recipe.MachineRecipeGuid.ToString(), InheritedLevel = pending.InheritedLevel,
                Outputs = pending.Outputs.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                TotalTicks = pending.TotalTicks, PowerMultiplier = pending.PowerMultiplier, DrawRecord = drawRecord,
            };
        }

        internal static void WarnIfLegacy(JObject saved)
        {
            if (saved != null && saved.Property("pendingRealization") == null)
                Debug.LogWarning("旧機械セーブに確定結果の節が無いため待機中の抽選は未確定として復元します");
        }

        internal MachineRealization Restore(IRealizedOutputDrawRecord drawRecord, MachineModuleEffectComponent effect)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(RecipeGuid));
            if (recipe == null)
            {
                Debug.LogWarning($"確定済み加工のレシピが無いため待機抽選を破棄します recipe:{RecipeGuid}");
                return null;
            }
            if (!MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(recipe, InheritedLevel))
            {
                Debug.LogWarning($"現行レシピと継承設定が異なるため待機抽選を破棄します recipe:{RecipeGuid}");
                return null;
            }
            if (TotalTicks == 0 || Outputs == null)
                throw new InvalidOperationException($"確定済み加工の保存値が不正です recipe:{RecipeGuid}");
            // 旧保存だけ現在の効果で補正し、確定時間や抽選は取り直さない
            // Only legacy saves recover power from current effects without rerolling time or outputs
            var restoredPower = PowerMultiplier;
            if (!restoredPower.HasValue)
            {
                restoredPower = effect.AggregateCurrent().PowerMultiplier;
                Debug.LogWarning($"旧待機抽選に確定電力倍率が無いため現在の効果から復元します recipe:{RecipeGuid} powerMultiplier:{restoredPower}");
            }
            if (float.IsNaN(restoredPower.Value) || float.IsInfinity(restoredPower.Value) || restoredPower.Value <= 0f)
                throw new InvalidOperationException($"確定済み加工の電力倍率が不正です recipe:{RecipeGuid}");
            return new MachineRealization(recipe, InheritedLevel, Outputs.Select(item => item.ToItemStack()).ToList(), TotalTicks, restoredPower.Value, drawRecord);
        }
    }
}
