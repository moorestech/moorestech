using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     撤去1バッチの楽観記録レコード
    ///     Optimistic record of one remove batch
    /// </summary>
    public class RemoveOperationRecord : IBuildOperationRecord
    {
        private readonly List<RemovedBlockInfo> _removedBlocks;

        private RemoveOperationRecord(List<RemovedBlockInfo> removedBlocks)
        {
            _removedBlocks = removedBlocks;
        }

        /// <summary>
        ///     有効セルが1件以上あるか（空バッチをPushしないためのガード）
        ///     Whether the record has any cells (guards against pushing an empty batch)
        /// </summary>
        public bool HasCells => 0 < _removedBlocks.Count;

        /// <summary>
        ///     削除対象からブロック分だけを楽観的にスナップショットする（撤去失敗セルはUndo時の占有ガードで自然に無効化）
        ///     Optimistically snapshot block targets only (failed removals are neutralized by the occupancy guard on undo)
        /// </summary>
        public static RemoveOperationRecord CreateFrom(List<IDeleteTarget> deleteTargets)
        {
            var removedBlocks = new List<RemovedBlockInfo>();
            foreach (var target in deleteTargets)
            {
                if (target is not BlockGameObjectChild blockChild) continue;
                var blockGameObject = blockChild.BlockGameObject;
                removedBlocks.Add(new RemovedBlockInfo(
                    blockGameObject.BlockPosInfo.OriginalPos,
                    blockGameObject.BlockId,
                    blockGameObject.BlockPosInfo.BlockDirection));
            }
            return new RemoveOperationRecord(removedBlocks);
        }

        /// <summary>
        ///     撤去の取り消し。占有範囲が空いているセルだけを1バッチで再設置する（CreateParamsは復元不可のため空）
        ///     Undo the removal by re-placing only cells whose footprint is unoccupied, in one batch (CreateParams cannot be restored, so empty)
        /// </summary>
        public UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var placeInfos = new List<PlaceInfo>();
            foreach (var removed in _removedBlocks)
            {
                // 占有中のセルは再設置しない（撤去失敗・他者設置セルを除外）
                // Skip occupied cells (excludes failed removals and rebuilt cells)
                if (IsFootprintOccupied(removed)) continue;
                placeInfos.Add(new PlaceInfo
                {
                    Position = removed.Position,
                    Direction = removed.Direction,
                    VerticalDirection = ToVerticalDirection(removed.Direction),
                    BlockId = removed.BlockId,
                    Placeable = true,
                });
            }
            if (placeInfos.Count != 0) ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos);
            return UniTask.CompletedTask;

            #region Internal

            bool IsFootprintOccupied(RemovedBlockInfo removed)
            {
                // 辞書キーはオリジン座標のみのため、マルチセルブロックとの重なりは占有範囲同士で判定する
                // The dictionary keys origins only, so overlap with multi-cell blocks needs a footprint check
                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(removed.BlockId).BlockSize;
                var positionInfo = new BlockPositionInfo(removed.Position, removed.Direction, blockSize);
                return blockGameObjectDataStore.IsOverlapPositionInfo(positionInfo);
            }

            static BlockVerticalDirection ToVerticalDirection(BlockDirection direction)
            {
                return direction switch
                {
                    BlockDirection.UpNorth or BlockDirection.UpEast or BlockDirection.UpSouth or BlockDirection.UpWest => BlockVerticalDirection.Up,
                    BlockDirection.DownNorth or BlockDirection.DownEast or BlockDirection.DownSouth or BlockDirection.DownWest => BlockVerticalDirection.Down,
                    _ => BlockVerticalDirection.Horizontal,
                };
            }

            #endregion
        }
    }

    public readonly struct RemovedBlockInfo
    {
        public readonly Vector3Int Position;
        public readonly BlockId BlockId;
        public readonly BlockDirection Direction;

        public RemovedBlockInfo(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            Position = position;
            BlockId = blockId;
            Direction = direction;
        }
    }
}
