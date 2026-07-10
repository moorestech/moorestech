using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Game.Block.Blocks.Machine;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom.Machine
{
    public class CleanRoomMachineProcessorSaveJsonObject
    {
        [JsonProperty("cycleCount")] public uint CycleCount;
        [JsonProperty("state")] public int State;
        [JsonProperty("totalSeconds")] public double TotalSeconds;
        [JsonProperty("remainingSeconds")] public double RemainingSeconds;
        [JsonProperty("recipeGuid")] public string RecipeGuidStr;
        [JsonProperty("pendingOutputs")] public List<ItemStackSaveJsonObject> PendingOutputs;
        [JsonProperty("pendingFluidOutputs")] public List<MachineFluidStackSaveJsonObject> PendingFluidOutputs;
        [JsonProperty("consumedItems")] public List<ItemStackSaveJsonObject> ConsumedItems;
        [JsonProperty("inputSlot")] public List<ItemStackSaveJsonObject> InputSlot;
        [JsonProperty("outputSlot")] public List<ItemStackSaveJsonObject> OutputSlot;
        [JsonProperty("moduleSlot")] public List<ItemStackSaveJsonObject> ModuleSlot;

        public Guid? GetRecipeGuid()
        {
            return RecipeGuidStr == null ? null : Guid.Parse(RecipeGuidStr);
        }
    }
}
