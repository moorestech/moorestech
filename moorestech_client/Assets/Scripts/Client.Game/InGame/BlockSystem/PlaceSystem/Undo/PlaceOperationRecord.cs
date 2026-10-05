using Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Game.Block.Interface;
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
                cells.Add(new PlacedCell(info.Position, info.Direction, info.BlockId));
            }
            return new PlaceOperationRecord(cells);
        }

        /// <summary>
        ///     設置の取り消し。同座標同BlockIdの現存セルだけを撤去する（設置失敗・他者変更セルの誤爆防止）
        ///     Undo the placement by removing only cells still holding the same BlockId (avoids nuking failed or replaced cells)
        /// </summary>
        public async UniTask UndoAsync(IBlockOccupancyQuery occupancy)
        {
            foreach (var cell in _cells)
            {
                if (!IsSameBlockAlive(cell))
                {
                    Debug.Log($"[PlaceUndo] skip cell: same block absent at {cell.Position}");
                    continue;
                }
                var response = await ClientContext.VanillaApi.Response.Block.BlockRemove(cell.Position, CancellationToken.None);
                // 応答はタイムアウト・デコード失敗でnullになる外部データ
                // The response is external data and becomes null on timeout or decode failure
                if (response == null)
                {
                    Debug.LogWarning($"[PlaceUndo] remove got no response (timeout or decode failure) at {cell.Position}");
                    continue;
                }
                if (!response.Success) Debug.LogWarning($"[PlaceUndo] remove refused: {response.FailureReason} at {cell.Position}");
            }

            #region Internal

            bool IsSameBlockAlive(PlacedCell cell)
            {
                var size = MasterHolder.BlockMaster.GetBlockMaster(cell.BlockId).BlockSize;
                var footprint = new BlockPositionInfo(cell.Position, cell.Direction, size);
                return occupancy.GetOccupancy(footprint, cell.BlockId) == BlockFootprintOccupancy.SameBlockPresent;
            }

            #endregion
        }

        private readonly struct PlacedCell
        {
            public readonly Vector3Int Position;
            public readonly BlockDirection Direction;
            public readonly BlockId BlockId;

            public PlacedCell(Vector3Int position, BlockDirection direction, BlockId blockId)
            {
                Position = position;
                Direction = direction;
                BlockId = blockId;
            }
        }
    }
}
