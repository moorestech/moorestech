using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 経路セルを手持ちの水平ブロックと坂ブロックへ割り当てる
    /// Assigns path cells to the held horizontal block and slope blocks
    /// </summary>
    public static class BeltConveyorStraightCellBlockResolver
    {
        // beltReasonsはcellsと同じ添字で並走するベルト固有理由の列。坂ブロック欠落で不可になったセルはここへ書き戻す
        // beltReasons is the belt-specific reason column indexed like cells; cells blocked by a missing slope block are written back into it
        public static List<PlaceInfo> ResolveStraightRun(IReadOnlyList<PlaceInfo> cells, BlockId horizontalBlockId, BlockId? upBlockId, BlockId? downBlockId, IList<BeltConveyorPlacementBlockReason> beltReasons)
        {
            // 経路の各セルを縮約せず1ブロックへ変換する
            // Convert every path cell to one block without collapsing the path
            var result = new List<PlaceInfo>(cells.Count);
            for (var i = 0; i < cells.Count; i++) result.Add(ResolveCell(i));
            return result;

            #region Internal

            PlaceInfo ResolveCell(int cellIndex)
            {
                var cell = cells[cellIndex];
                var blockId = horizontalBlockId;
                var placeable = cell.Placeable;

                // 傾斜方向に対応する坂がなければ設置不可にする
                // Mark the cell unplaceable when its slope block is unavailable
                if (cell.VerticalDirection == BlockVerticalDirection.Up)
                    ResolveSlope(cellIndex, upBlockId, ref blockId, ref placeable);
                if (cell.VerticalDirection == BlockVerticalDirection.Down)
                    ResolveSlope(cellIndex, downBlockId, ref blockId, ref placeable);

                return new PlaceInfo
                {
                    Position = cell.Position,
                    Direction = cell.Direction,
                    VerticalDirection = cell.VerticalDirection,
                    Placeable = placeable,
                    BlockId = blockId,
                };
            }

            void ResolveSlope(int cellIndex, BlockId? slopeBlockId, ref BlockId blockId, ref bool placeable)
            {
                if (slopeBlockId.HasValue)
                {
                    blockId = slopeBlockId.Value;
                    return;
                }

                // 先に立った原因を優先する（先に不可なら坂欠落は後追いの理由でしかない）
                // The earlier cause wins; an already-blocked cell only gains the slope gap as a follow-on reason
                if (placeable) beltReasons[cellIndex] = BeltConveyorPlacementBlockReason.SlopeBlockMissing;
                placeable = false;
            }

            #endregion
        }
    }
}
