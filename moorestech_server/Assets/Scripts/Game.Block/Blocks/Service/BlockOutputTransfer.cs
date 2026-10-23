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
            {
                var item = slots.GetItem(slot);
                // silent-ok: 空枠には搬出物が無い / 在庫UIが空状態を表示する
                if (item.Count == 0) continue;
                slots.SetItem(slot, destination.InsertItem(item));
            }
        }
    }
}
