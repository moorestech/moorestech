using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using Client.Game.InGame.UI.UIState.State;
using Core.Master;
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
        private readonly IRemovalRestoreSender _sender;

        private RemoveOperationRecord(List<IRemovedObject> removedObjects, int unrecordableCount, IRemovalRestoreSender sender)
        {
            _removedObjects = removedObjects;
            _unrecordableCount = unrecordableCount;
            _sender = sender;
        }

        // 空バッチをPushしないためのガード。記録できなかった物だけでも、Undo時に通知するため積む
        // Guard against pushing an empty batch; a batch of only unrecordable things is still pushed so undo can report them
        public bool HasRemovedObjects => 0 < _removedObjects.Count || 0 < _unrecordableCount;

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
            return new RemoveOperationRecord(unique, collector.UnrecordableCount, sender);
        }

        public UniTask UndoAsync(IBlockOccupancyQuery occupancy)
        {
            // ブロック相: 空きセルを一括再設置
            // Block phase: re-place free cells in one batch
            var placeInfos = new List<PlaceInfo>();
            var skippedCount = _unrecordableCount;
            foreach (var removedObject in _removedObjects)
            {
                if (removedObject.AppendBlockRestore(placeInfos, occupancy) == BlockRestoreOutcome.SkippedOccupied) skippedCount++;
            }
            if (placeInfos.Count != 0) _sender.PlaceBlocks(placeInfos);

            // 接続相: 再設置の後に線を引き直す（サーバーFIFOで再設置が先に適用される）
            // Connection phase: re-draw lines after the re-place (server FIFO applies the re-place first)
            foreach (var removedObject in _removedObjects) removedObject.SendConnectionRestore(_sender);

            // クライアント側で戻せなかった分はプレイヤーへ件数で知らせる（裁定: できた分だけ戻し残りは通知）
            // Report what the client could not restore to the player by count (ruling: restore what we can, notify the rest)
            if (0 < skippedCount) _sender.NotifyRestoreSkipped(skippedCount);
            return UniTask.CompletedTask;
        }
    }
}
