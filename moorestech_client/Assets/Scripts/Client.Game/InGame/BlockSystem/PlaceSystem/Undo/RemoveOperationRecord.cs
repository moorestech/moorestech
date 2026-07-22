using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
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

        public RemoveOperationRecord(List<RemovedBlockInfo> removedBlocks)
        {
            _removedBlocks = removedBlocks;
        }

        /// <summary>
        ///     撤去の取り消し。占有範囲が空いているセルだけを1バッチで再設置する（CreateParamsは復元不可のため空）
        ///     Undo the removal by re-placing only cells whose footprint is unoccupied, in one batch (CreateParams cannot be restored, so empty)
        /// </summary>
        public UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var cells = SelectReplaceableCells(IsFootprintOccupied);
            if (cells.Count == 0) return UniTask.CompletedTask;

            var placeInfos = new List<PlaceInfo>(cells.Count);
            foreach (var cell in cells)
            {
                placeInfos.Add(new PlaceInfo
                {
                    Position = cell.Position,
                    Direction = cell.Direction,
                    VerticalDirection = ToVerticalDirection(cell.Direction),
                    BlockId = cell.BlockId,
                    Placeable = true,
                });
            }
            ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos);
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

        /// <summary>
        ///     占有されていないセルだけを再設置対象として返す（撤去失敗・他者設置セルを除外）
        ///     Return only unoccupied cells (excludes failed removals and rebuilt cells)
        /// </summary>
        public List<RemovedBlockInfo> SelectReplaceableCells(Func<RemovedBlockInfo, bool> isOccupied)
        {
            var result = new List<RemovedBlockInfo>();
            foreach (var removed in _removedBlocks)
            {
                if (isOccupied(removed)) continue;
                result.Add(removed);
            }
            return result;
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
