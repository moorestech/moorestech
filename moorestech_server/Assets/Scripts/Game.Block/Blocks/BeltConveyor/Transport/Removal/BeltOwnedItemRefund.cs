using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.Context;

namespace Game.Block.Blocks.BeltConveyor.Transport.Removal
{
    internal static class BeltOwnedItemRefund
    {
        internal static IReadOnlyList<IItemStack> GetItems(BlockInstanceId blockInstanceId, BeltTransportSnapshot snapshot, IReadOnlyDictionary<BlockInstanceId, BeltConveyorSaveJsonObject> loadedStates)
        {
            // pendingは組と重ねず読む
            // Read pending state without overlapping the assembly
            if (loadedStates.TryGetValue(blockInstanceId, out var pending))
            {
                snapshot = new BeltTransportSnapshot();
                BeltTransportLoadedStateConverter.Convert(new Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> { [blockInstanceId] = pending }, snapshot);
            }

            // 再構築と同じ所有規則で現在の品を集める
            // Collect current items using the rebuild ownership rules
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
