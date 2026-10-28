using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
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
        [JsonProperty("drawRecord")] public JObject DrawRecord;

        internal static MachineRealizationSaveJsonObject Capture(IdleMachineProcessState idle, JObject drawRecord)
        {
            if (!idle.TryGetPendingRealization(out var pending)) return null;
            return new MachineRealizationSaveJsonObject
            {
                RecipeGuid = pending.Recipe.MachineRecipeGuid.ToString(), InheritedLevel = pending.InheritedLevel,
                Outputs = pending.Outputs.Select(item => new ItemStackSaveJsonObject(item)).ToList(),
                TotalTicks = pending.TotalTicks, DrawRecord = drawRecord,
            };
        }

        internal static void WarnIfLegacy(JObject saved)
        {
            if (saved != null && saved.Property("pendingRealization") == null)
                Debug.LogWarning("旧機械セーブに確定結果の節が無いため待機中の抽選は未確定として復元します");
        }

        internal MachineRealization Restore(IRealizedOutputDrawRecord drawRecord)
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse(RecipeGuid));
            if (recipe == null)
            {
                Debug.LogWarning($"確定済み加工のレシピが無いため待機抽選を破棄します recipe:{RecipeGuid}");
                return null;
            }
            if (TotalTicks == 0 || Outputs == null || !MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(recipe, InheritedLevel))
                throw new InvalidOperationException($"確定済み加工の保存値が不正です recipe:{RecipeGuid}");
            return new MachineRealization(recipe, InheritedLevel, Outputs.Select(item => item.ToItemStack()).ToList(), TotalTicks, drawRecord);
        }
    }
}
