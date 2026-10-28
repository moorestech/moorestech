using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Fluid;
using Newtonsoft.Json;
using Game.Block.Blocks.Machine.State.Save;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    public class CleanRoomMachineProcessorSaveJsonObject
    {
        // V8では確定時間・消費実品・待機抽選をそのまま保存する
        // V8 preserves fixed time, consumed identities and waiting draws
        [JsonProperty("totalTicks")] public uint? TotalTicks;
        [JsonProperty("powerMultiplier")] public float? PowerMultiplier;
        [JsonProperty("consumedInputItemGuids")] public List<string> ConsumedInputItemGuids;
        [JsonProperty("pendingRealization")] public MachineRealizationSaveJsonObject PendingRealization;

        [JsonProperty("cycleCount")] public uint CycleCount;
        [JsonProperty("state")] public int State;
        [JsonProperty("remainingSeconds")] public double RemainingSeconds;
        [JsonProperty("recipeGuid")] public string RecipeGuidStr;
        [JsonIgnore] public Guid RecipeGuid => Guid.Parse(RecipeGuidStr);
        [JsonProperty("pendingOutputs")] public List<ItemStackSaveJsonObject> PendingOutputs;
        [JsonProperty("inputSlot")] public List<ItemStackSaveJsonObject> InputSlot;
        [JsonProperty("outputSlot")] public List<ItemStackSaveJsonObject> OutputSlot;
        [JsonProperty("moduleSlot")] public List<ItemStackSaveJsonObject> ModuleSlot;

        // 選択中レシピ。未選択はnull（旧セーブもキー無し=null）
        // Selected recipe; null when unselected (older saves lack the key)
        [JsonProperty("selectedRecipeGuid")] public string SelectedRecipeGuidStr;

        // 加工中ジョブの継承レベル（継承レシピは1以上。ジョブ無しと継承でないレシピは null）
        // Inherited level of the running job (1 or more for an inheritance recipe; null otherwise)
        [JsonProperty("inheritedLevel")] public int? InheritedLevel;

        // 液体タンク。版8から必ずある（版7以前はマイグレーションで空配列になる）
        // Fluid tanks; always present from version 8 (version 7 and older get empty arrays via migration)
        [JsonProperty("inputFluidSlot")] public List<FluidContainerSaveJsonObject> InputFluidSlot;
        [JsonProperty("outputFluidSlot")] public List<FluidContainerSaveJsonObject> OutputFluidSlot;
    }
}
