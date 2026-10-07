using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.UI.UIState.State;
using Game.Block.Interface;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     撤去1バッチの楽観記録（ブロック・線・レール）
    ///     Optimistic record of one remove batch (blocks, lines, rails)
    /// </summary>
    public class RemoveOperationRecord : IBuildOperationRecord
    {
        private readonly List<IRemovedObject> _removedObjects;
        private readonly int _unrecordableCount;
        private readonly List<RemovedObjectCollector.UnrecordableBlock> _unrecordableBlocks;
        private readonly IRemovalRestoreSender _sender;

        private RemoveOperationRecord(List<IRemovedObject> removedObjects, int unrecordableCount, List<RemovedObjectCollector.UnrecordableBlock> unrecordableBlocks, IRemovalRestoreSender sender)
        {
            _removedObjects = removedObjects;
            _unrecordableCount = unrecordableCount;
            _unrecordableBlocks = unrecordableBlocks;
            _sender = sender;
        }

        // 空バッチをPushしないためのガード。記録できなかった物だけでも、Undo時に通知するため積む
        // Guard against pushing an empty batch; a batch of only unrecordable things is still pushed so undo can report them
        public bool HasRemovedObjects => 0 < _removedObjects.Count || 0 < _unrecordableCount || 0 < _unrecordableBlocks.Count;

        // 撤去直前の各対象から撤去物を集め、論理キーで重複排除する
        // Collect removed objects from every target right before removal, deduped by logical key
        public static RemoveOperationRecord CreateFrom(IReadOnlyList<IDeleteTarget> targets, IRemovalRestoreSender sender)
        {
            var collector = new RemovedObjectCollector();
            foreach (var target in targets) target.CollectRemovedObjects(collector);

            var seenKeys = new HashSet<object>();
            var unique = new List<IRemovedObject>();
            foreach (var removedObject in collector.Objects)
            {
                if (seenKeys.Add(removedObject.RestoreKey)) unique.Add(removedObject);
            }
            return new RemoveOperationRecord(unique, collector.UnrecordableCount, new List<RemovedObjectCollector.UnrecordableBlock>(collector.GetUnrecordableBlocks()), sender);
        }

        public UniTask UndoAsync(IBlockOccupancyQuery occupancy)
        {
            // ブロック相: 空きセルを一括再設置
            // Block phase: re-place free cells in one batch
            var placeInfos = new List<PlaceInfo>();
            var skippedBlockPositions = new HashSet<Vector3Int>();
            var skippedCount = _unrecordableCount;
            foreach (var removedObject in _removedObjects)
            {
                if (removedObject.AppendBlockRestore(placeInfos, occupancy, skippedBlockPositions) == BlockRestoreOutcome.SkippedOccupied) skippedCount++;
            }
            // 記録不能ブロックは撤去に失敗して現存する場合だけ件数から除く
            // Exclude an unrecordable block only if removal failed and it remains present
            foreach (var block in _unrecordableBlocks)
            {
                if (occupancy.GetOccupancy(block.Position, block.Direction, block.BlockId) == BlockFootprintOccupancy.SameBlockPresent)
                {
                    Debug.Log($"[RemovalRestore] unrecordable block still present at {block.Position}");
                    continue;
                }
                skippedBlockPositions.Add(block.Position);
                skippedCount++;
            }
            if (placeInfos.Count != 0) _sender.PlaceBlocks(placeInfos);

            // 接続相: 再設置の後に線を引き直す（サーバーFIFOで再設置が先に適用される）。戻らなかったブロックの端点へは引かない
            // Connection phase: re-draw lines after the re-place (server FIFO applies it first); never toward an endpoint whose block was not restored
            foreach (var removedObject in _removedObjects)
            {
                if (!removedObject.TrySendConnectionRestore(_sender, skippedBlockPositions)) skippedCount++;
            }

            // クライアント側で戻せなかった分はプレイヤーへ件数で知らせる（裁定: できた分だけ戻し残りは通知）
            // Report what the client could not restore to the player by count (ruling: restore what we can, notify the rest)
            if (0 < skippedCount) _sender.NotifyRestoreSkipped(skippedCount);
            return UniTask.CompletedTask;
        }
    }
}
