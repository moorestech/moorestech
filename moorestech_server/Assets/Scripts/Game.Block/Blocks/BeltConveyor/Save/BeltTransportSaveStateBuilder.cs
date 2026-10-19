using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // 再構築と同じ取り出し結果(snapshot)を、blockごとの保存形へ振り分ける。アイテムのマスとマス内距離はsnapshotがsegment単位で確定済み
    // Distributes the same capture a rebuild uses (the snapshot) into per-block save forms; each item's cell and in-cell distance are already settled per segment by the snapshot
    public static class BeltTransportSaveStateBuilder
    {
        public static Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject> Build(BeltTransportSnapshot snapshot)
        {
            var states = new Dictionary<BlockInstanceId, BeltConveyorSaveJsonObject>();

            // 走行中は先頭が乗るマスのblockへ出口に近い順のまま、bufferと優先順は末尾マスのblockへ、内部segmentは合流blockへ入力方向つきで入れる
            // Running items go to the block under their head in exit order, buffers and orders to the last cell's block, internal-segment items to the merge block with their input direction
            foreach (var record in snapshot.RunningItems)
                StateOf(record.CellBlockInstanceId).Items.Add(new BeltItemSaveJsonObject(record.Item, record.DistanceToCellExit));
            foreach (var pair in snapshot.PriorityOrders) StateOf(pair.Key).PriorityOrder = pair.Value;
            foreach (var record in snapshot.BufferItems) StateOf(record.CellBlockInstanceId).BufferItem = new BeltItemSaveJsonObject(record.Item, 0);
            foreach (var record in snapshot.InternalItems)
            {
                var internalItems = StateOf(record.MergeBlockInstanceId).InternalItems;
                foreach (var captured in record.States) internalItems.Add(new BeltInternalItemSaveJsonObject(record.InputDirection, captured.Item, captured.DistanceToExit));
            }
            return states;

            #region Internal

            BeltConveyorSaveJsonObject StateOf(BlockInstanceId blockInstanceId)
            {
                if (states.TryGetValue(blockInstanceId, out var state)) return state;
                state = new BeltConveyorSaveJsonObject();
                states.Add(blockInstanceId, state);
                return state;
            }

            #endregion
        }
    }
}
