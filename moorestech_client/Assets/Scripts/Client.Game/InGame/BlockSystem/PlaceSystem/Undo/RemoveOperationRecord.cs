using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
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
