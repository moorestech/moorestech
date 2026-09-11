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
    ///     張替え1バッチの履歴レコード。Undoは同セルを旧BlockIdへ張り戻す逆張替え
    ///     History record of one replace batch; undo replaces the same cells back to the old BlockId
    /// </summary>
    public class ReplaceOperationRecord : IBuildOperationRecord
    {
        private readonly List<ReplacedCell> _cells;

        private ReplaceOperationRecord(List<ReplacedCell> cells)
        {
            _cells = cells;
        }

        /// <summary>
        ///     有効セルが1件以上あるか（空バッチをPushしないためのガード）
        ///     Whether the record has any cells (guards against pushing an empty batch)
        /// </summary>
        public bool HasCells => 0 < _cells.Count;

        // 送信前に既設のBlockIdを旧ブロックとして控える（送信後は既に差し替わっている）
        // Capture the existing BlockId as the old block before sending (it is already replaced afterwards)
        // 呼び出し側が設置可能セルへ絞っていなくても正しく動く（絞り込みは送信側の都合で、記録の条件はここが持つ）
        // Correct even when the caller has not narrowed to placeable cells; that narrowing belongs to the sender, the record's own condition lives here
        public static ReplaceOperationRecord CreateFrom(List<PlaceInfo> placeInfos, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var cells = new List<ReplacedCell>(placeInfos.Count);
            foreach (var placeInfo in placeInfos)
            {
                if (!placeInfo.Placeable || !placeInfo.IsReplace) continue;
                if (!blockGameObjectDataStore.TryGetBlockGameObject(placeInfo.Position, out var existing)) continue;
                cells.Add(new ReplacedCell(placeInfo.Position, placeInfo.Direction, placeInfo.VerticalDirection, existing.BlockId, placeInfo.BlockId));
            }
            return new ReplaceOperationRecord(cells);
        }

        public UniTask UndoAsync(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var placeInfos = BuildUndoPlaceInfos(blockGameObjectDataStore);
            if (placeInfos.Count != 0) ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos);
            return UniTask.CompletedTask;
        }

        // 現在も新BlockIdのセルだけを旧BlockIdへ戻す（他者変更セルの誤爆防止）
        // Only cells still holding the new BlockId go back to the old one (avoids clobbering cells changed by others)
        internal List<PlaceInfo> BuildUndoPlaceInfos(BlockGameObjectDataStore blockGameObjectDataStore)
        {
            var placeInfos = new List<PlaceInfo>();
            foreach (var cell in _cells)
            {
                if (!blockGameObjectDataStore.TryGetBlockGameObject(cell.Position, out var current)) continue;
                if (current.BlockId != cell.NewBlockId) continue;
                placeInfos.Add(new PlaceInfo
                {
                    Position = cell.Position,
                    Direction = cell.Direction,
                    VerticalDirection = cell.VerticalDirection,
                    BlockId = cell.OldBlockId,
                    IsReplace = true,
                    Placeable = true,
                });
            }
            return placeInfos;
        }

        private readonly struct ReplacedCell
        {
            public readonly Vector3Int Position;
            public readonly BlockDirection Direction;
            public readonly BlockVerticalDirection VerticalDirection;
            public readonly BlockId OldBlockId;
            public readonly BlockId NewBlockId;

            // 入れ子structはprivateなので外部からは見えない。ctorをprivateにすると外側クラスから呼べずCS0122になる
            // The nested struct is already private to the outside; a private ctor would be unreachable from the enclosing class (CS0122)
            public ReplacedCell(Vector3Int position, BlockDirection direction, BlockVerticalDirection verticalDirection, BlockId oldBlockId, BlockId newBlockId)
            {
                Position = position;
                Direction = direction;
                VerticalDirection = verticalDirection;
                OldBlockId = oldBlockId;
                NewBlockId = newBlockId;
            }
        }
    }
}
