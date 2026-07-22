using System;
using System.Collections.Generic;
using Core.Master;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     設置1バッチの履歴レコード。送信時のPlaceInfoをスナップショットとして保持する
    ///     History record of one place batch; holds a snapshot of the sent PlaceInfo list
    /// </summary>
    public class PlaceOperationRecord : IBuildOperationRecord
    {
        private readonly List<PlacedCell> _cells;

        private PlaceOperationRecord(List<PlacedCell> cells)
        {
            _cells = cells;
        }

        /// <summary>
        ///     有効セルが1件以上あるか（空バッチをPushしないためのガード）
        ///     Whether the record has any cells (guards against pushing an empty batch)
        /// </summary>
        public bool HasCells => _cells.Count > 0;

        public static PlaceOperationRecord CreateFrom(List<PlaceInfo> placeInfos)
        {
            // 設置システムはPlaceInfoを使い回すため値をコピーして保持する
            // Placement systems reuse PlaceInfo instances, so copy the values we need
            var cells = new List<PlacedCell>(placeInfos.Count);
            foreach (var info in placeInfos)
            {
                if (!info.Placeable) continue;
                cells.Add(new PlacedCell(info.Position, info.BlockId));
            }
            return new PlaceOperationRecord(cells);
        }

        /// <summary>
        ///     同座標に同BlockIdのブロックが現存するセルだけをUndo対象として返す
        ///     Return only cells whose position still holds a block with the same BlockId
        /// </summary>
        public List<Vector3Int> SelectUndoableCells(Func<Vector3Int, BlockId?> blockIdAt)
        {
            var result = new List<Vector3Int>();
            foreach (var cell in _cells)
            {
                var currentBlockId = blockIdAt(cell.Position);
                if (currentBlockId == null || !currentBlockId.Value.Equals(cell.BlockId)) continue;
                result.Add(cell.Position);
            }
            return result;
        }

        private readonly struct PlacedCell
        {
            public readonly Vector3Int Position;
            public readonly BlockId BlockId;

            public PlacedCell(Vector3Int position, BlockId blockId)
            {
                Position = position;
                BlockId = blockId;
            }
        }
    }
}
