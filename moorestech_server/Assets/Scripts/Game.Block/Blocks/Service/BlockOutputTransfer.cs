using System.Collections.Generic;
using Core.Inventory;
using Game.Block.Blocks.Connector;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Service
{
    // 出力枠の搬送と余りの確定を共有し、束縛規則は具体側へ残す
    // Share output transfer and remainder commits while keeping slot binding in callers
    internal static class BlockOutputTransfer
    {
        internal static void Transfer(OpenableInventoryItemDataStoreService slots, IReadOnlyList<int> outputSlots, IBlockInventoryInserter destination)
        {
            foreach (var slot in outputSlots)
                slots.SetItem(slot, destination.InsertItem(slots.GetItem(slot)));
        }
    }
}
