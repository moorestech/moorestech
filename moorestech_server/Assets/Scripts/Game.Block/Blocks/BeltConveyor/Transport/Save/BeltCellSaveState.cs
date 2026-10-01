using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    public sealed class BeltCellSaveState
    {
        [JsonProperty("priorityOrder")] public int PriorityOrder;
        [JsonProperty("items")] public List<BeltCellSavedItem> Items;
        [JsonProperty("bufferItem")] public BeltCellSavedItem BufferItem;
    }
    public sealed class BeltCellSavedItem
    {
        [JsonProperty("itemStack")] public ItemStackSaveJsonObject ItemStack;
        [JsonProperty("instanceId")] public long InstanceId;
        [JsonProperty("progress")] public int Progress;
        [JsonProperty("entryDirection")] public int EntryDirection;
        [JsonProperty("entryHeight")] public int EntryHeight;
    }
}
