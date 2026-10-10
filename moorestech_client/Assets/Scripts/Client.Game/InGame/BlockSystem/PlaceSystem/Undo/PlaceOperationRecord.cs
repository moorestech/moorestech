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

        public static PlaceOperationRecord CreateFromPlacedCells(List<BlueprintPlacedCellMessagePack> placedCells)
        {
            // サーバーが確定した個体だけをUndoへ登録する
            // Register only instances confirmed by the server for undo
            var cells = new List<PlacedCell>(placedCells.Count);
            foreach (var cell in placedCells)
            {
                cells.Add(new PlacedCell(cell.Position.Vector3Int, (BlockDirection)cell.Direction,
                    (BlockId)cell.BlockId, new BlockInstanceId(cell.BlockInstanceId)));
            }
            return new PlaceOperationRecord(cells);
        }

        /// <summary>
        ///     設置を取り消す。BPはサーバー個体ID、通常設置は占有を照合する
        ///     Undo placement using server instance IDs for blueprints and occupancy for normal placement
        /// </summary>
        public async UniTask UndoAsync(IBlockOccupancyQuery occupancy)
        {
            foreach (var cell in _cells)
            {
                if (!cell.ExpectedInstanceId.HasValue &&
                    occupancy.GetOccupancy(cell.Position, cell.Direction, cell.BlockId) != BlockFootprintOccupancy.SameBlockPresent)
                {
                    Debug.Log($"[PlaceUndo] skip cell: same block absent at {cell.Position}");
                    continue;
                }
                // BPはサーバーの個体ID照合で撤去競合を閉じる
                // Server-side instance matching closes the blueprint undo race
                var response = cell.ExpectedInstanceId.HasValue
                    ? await ClientContext.VanillaApi.Response.Block.BlockRemoveIfInstance(cell.Position, cell.ExpectedInstanceId.Value, CancellationToken.None)
                    : await ClientContext.VanillaApi.Response.Block.BlockRemove(cell.Position, CancellationToken.None);
                // 応答はタイムアウト・デコード失敗でnullになる外部データ
                // The response is external data and becomes null on timeout or decode failure
                if (response == null)
                {
                    Debug.LogWarning($"[PlaceUndo] remove got no response (timeout or decode failure) at {cell.Position}");
                    continue;
                }
                if (!response.Success) Debug.LogWarning($"[PlaceUndo] remove refused: {response.FailureReason} at {cell.Position}");
            }

        }

        private readonly struct PlacedCell
        {
            public readonly Vector3Int Position;
            public readonly BlockDirection Direction;
            public readonly BlockId BlockId;
            public readonly BlockInstanceId? ExpectedInstanceId;

            public PlacedCell(Vector3Int position, BlockDirection direction, BlockId blockId)
            {
                Position = position;
                Direction = direction;
                BlockId = blockId;
                ExpectedInstanceId = null;
            }

            public PlacedCell(Vector3Int position, BlockDirection direction, BlockId blockId, BlockInstanceId expectedInstanceId)
            {
                Position = position;
                Direction = direction;
                BlockId = blockId;
                ExpectedInstanceId = expectedInstanceId;
            }
        }
    }
}
