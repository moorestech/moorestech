using System.Collections.Generic;
using Core.BeltTransport;
using Newtonsoft.Json;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // ベルコンblock1つの保存形。先頭がこのマスにある走行中アイテム、合流・分岐なら優先順とbuffer内アイテム、合流なら内部segment上のアイテム
    // Save form of one belt block: running items whose head is on this cell, the priority order and buffered item for merges and branches, and internal-segment items for merges
    // segment・接続・隙間・密着はロード時の再構築で作り直すので保存しない
    // Segments, links, gaps and packing are rebuilt on load and therefore not saved
    public class BeltConveyorSaveJsonObject
    {
        [JsonProperty("items")] public List<BeltItemSaveJsonObject> Items = new();
        // 合流・分岐でないblockはInitializeFromDirection(-1)
        // InitializeFromDirection (-1) for blocks that are neither a merge nor a branch
        [JsonProperty("priorityOrder")] public int PriorityOrder = BeltPriority.InitializeFromDirection;
        // bufferが空、または合流・分岐でなければnull
        // null when the buffer is empty or the block is neither a merge nor a branch
        [JsonProperty("bufferItem")] public BeltItemSaveJsonObject BufferItem;
        [JsonProperty("internalItems")] public List<BeltInternalItemSaveJsonObject> InternalItems = new();
    }
}
