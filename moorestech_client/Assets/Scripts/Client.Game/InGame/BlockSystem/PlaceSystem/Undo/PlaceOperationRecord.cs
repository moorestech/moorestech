using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     設置1バッチの履歴レコード
    ///     History record of one place batch
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
        public bool HasCells => 0 < _cells.Count;

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
        ///     設置の取り消し。同座標同BlockIdの現存セルだけを撤去する（設置失敗・他者変更セルの誤爆防止）
        ///     Undo the placement by removing only cells still holding the same BlockId (avoids nuking failed or replaced cells)
        /// </summary>
        public async UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var cells = SelectUndoableCells(GetBlockIdAt);
            foreach (var position in cells)
            {
                await ClientContext.VanillaApi.Response.BlockRemove(position, CancellationToken.None);
            }

            #region Internal

            BlockId? GetBlockIdAt(Vector3Int position)
            {
                if (!blockGameObjectDataStore.TryGetBlockGameObject(position, out var blockGameObject)) return null;
                return blockGameObject.BlockId;
            }

            #endregion
        }

        /// <summary>
        ///     同座標同BlockId現存セルのみ返す
        ///     Return only cells still holding the same BlockId
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
