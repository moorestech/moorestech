using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    public class CleanRoomMachineProcessorSaveJsonObject
    {
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
    }
}
