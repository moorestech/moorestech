using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.Context;

namespace Game.Block.Blocks.BeltConveyor.Transport.Removal
{
    // ベルコンblockが所有するアイテムを返却物にする。所有の規則は再構築・セーブと同じ(走行中は先頭のマス、bufferは末尾マス、内部segmentは合流マス)
    // Turns the items a belt block owns into refunds; ownership follows the rebuild and save rules (running: head cell, buffer: last cell, internal segment: merge cell)
    internal static class BeltOwnedItemRefund
    {
        internal static IReadOnlyList<IItemStack> GetItems(BlockInstanceId blockInstanceId, BeltTransportSnapshot snapshot)
        {
            var items = new List<IItemStack>();
            foreach (var record in snapshot.RunningItems) Add(record.CellBlockInstanceId, record.Item);
            foreach (var record in snapshot.BufferItems) Add(record.CellBlockInstanceId, record.Item);
            foreach (var record in snapshot.InternalItems)
                foreach (var state in record.States) Add(record.MergeBlockInstanceId, state.Item);
            return items;

            #region Internal
            void Add(BlockInstanceId owner, BeltItem item)
            {
                if (owner == blockInstanceId)
                    items.Add(ServerContext.ItemStackFactory.Create(item.ItemId, 1, item.ItemInstanceId));
            }
            #endregion
        }
    }
}
