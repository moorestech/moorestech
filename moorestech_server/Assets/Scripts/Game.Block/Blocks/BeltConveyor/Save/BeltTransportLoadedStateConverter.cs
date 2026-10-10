using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Block.Blocks.BeltConveyor.Save
{
    // ロードした保存内容を再構築用の記録へ変換する。1つでも読めない値があるblockは壊れた扱いにし、そのblockのアイテムは1つも記録しない
    // Converts loaded save content into rebuild records; a block with any unreadable value counts as corrupted and none of its items are recorded
    public static class BeltTransportLoadedStateConverter
    {
        // 変換した走行中・buffer・内部segmentの記録をsnapshotへ足し、合流・分岐の保存済み優先順をblockごとに返す
        // Adds the converted running, buffer and internal records to the snapshot and returns the saved priority orders of merges and branches per block
        public static Dictionary<BlockInstanceId, int> Convert(IReadOnlyDictionary<BlockInstanceId, BeltConveyorSaveJsonObject> loadedStates, BeltTransportSnapshot snapshot)
        {
            var priorityOrders = new Dictionary<BlockInstanceId, int>();
            foreach (var pair in loadedStates)
            {
                if (!TryConvert(pair.Key, pair.Value, snapshot))
                {
                    // 壊れたセーブは異常。復元しない理由を残し、同じsegmentのアイテムを全て見送る
                    // A corrupted save is an anomaly; record why nothing is restored and drop every item of the same segment
                    Debug.LogError($"[BeltTransport] belt block {pair.Key} has unreadable saved items; no item of its segment is restored.");
                    snapshot.CorruptedBlocks.Add(pair.Key);
                    continue;
                }
                if (pair.Value.PriorityOrder != BeltPriority.InitializeFromDirection) priorityOrders.Add(pair.Key, pair.Value.PriorityOrder);
            }
            return priorityOrders;
        }

        private static bool TryConvert(BlockInstanceId block, BeltConveyorSaveJsonObject state, BeltTransportSnapshot snapshot)
        {
            // 先に全てを検証し、1つでも読めなければ何も記録しない
            // Validate everything first and record nothing when any value is unreadable
            if (state.Items == null || state.InternalItems == null) return false;
            var running = new List<BeltRunningItemRecord>();
            for (var i = 0; i < state.Items.Count; i++)
            {
                if (!TryReadItem(state.Items[i], out var item, out var distance)) return false;
                running.Add(new BeltRunningItemRecord(block, distance, item, -1, i));
            }

            var hasBufferItem = state.BufferItem != null;
            var bufferItem = default(BeltItem);
            if (hasBufferItem && !TryReadItem(state.BufferItem, out bufferItem, out _)) return false;

            // 内部segmentは入力方向ごとに1本・アイテム1個
            // One internal segment per input direction, holding at most one item
            var internals = new List<BeltInternalItemRecord>();
            var usedDirections = 0;
            foreach (var saved in state.InternalItems)
            {
                if (saved.InputDirection < (int)BeltDirection.Front || (int)BeltDirection.Right < saved.InputDirection) return false;
                if ((usedDirections & (1 << saved.InputDirection)) != 0) return false;
                usedDirections |= 1 << saved.InputDirection;
                if (saved.Item == null || !TryReadItem(saved.Item, out var item, out var distance)) return false;
                internals.Add(new BeltInternalItemRecord(block, (BeltDirection)saved.InputDirection, new[] { new BeltItemState(item, distance) }));
            }

            snapshot.RunningItems.AddRange(running);
            if (hasBufferItem) snapshot.BufferItems.Add(new BeltBufferItemRecord(block, bufferItem, -1));
            snapshot.InternalItems.AddRange(internals);
            return true;
        }

        private static bool TryReadItem(BeltItemSaveJsonObject saved, out BeltItem item, out int distanceToExit)
        {
            // 種類GUIDはマスタに存在し空でないこと、進入方向は12通り、距離は1マス内
            // The kind GUID must exist in the master and not be empty, the entry direction must be one of twelve, and the distance must lie within one cell
            item = default;
            distanceToExit = saved.DistanceToExit;
            if (!Guid.TryParse(saved.ItemGuidStr, out var itemGuid)) return false;
            var itemId = MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid);
            if (itemId == null || itemId.Value == ItemMaster.EmptyItemId) return false;
            if (saved.EntryDirection < (int)BeltEntryDirection.FromFront || (int)BeltEntryDirection.FromRightBelow < saved.EntryDirection) return false;
            if (distanceToExit < 0 || BeltConstants.ItemWidth <= distanceToExit) return false;
            item = new BeltItem(itemId.Value, ItemInstanceId.Create(), (BeltEntryDirection)saved.EntryDirection);
            return true;
        }
    }
}
