using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 経路セルをファミリーの直線・坂ブロックへ割り当てる
    /// Assigns path cells to the family's straight and slope blocks
    /// </summary>
    public static class BeltConveyorCellBlockResolver
    {
        public static List<PlaceInfo> Resolve(IReadOnlyList<PlaceInfo> cells, BeltConveyorFamily family)
        {
            // 経路の各セルを縮約せず1ブロックへ変換する
            // Convert every path cell to one block without collapsing the path
            var result = new List<PlaceInfo>(cells.Count);
            foreach (var cell in cells) result.Add(ResolveCell(cell));
            return result;

            #region Internal

            PlaceInfo ResolveCell(PlaceInfo cell)
            {
                var blockId = family.StraightBlockId;
                var placeable = cell.Placeable;

                // 傾斜方向に対応する坂がなければ設置不可にする
                // Mark the cell unplaceable when its slope block is unavailable
                if (cell.VerticalDirection == BlockVerticalDirection.Up)
                    ResolveSlope(family.UpBlockId, ref blockId, ref placeable);
                if (cell.VerticalDirection == BlockVerticalDirection.Down)
                    ResolveSlope(family.DownBlockId, ref blockId, ref placeable);

                return new PlaceInfo
                {
                    Position = cell.Position,
                    Direction = cell.Direction,
                    VerticalDirection = cell.VerticalDirection,
                    Placeable = placeable,
                    BlockId = blockId,
                };
            }

            void ResolveSlope(BlockId? slopeBlockId, ref BlockId blockId, ref bool placeable)
            {
                if (slopeBlockId.HasValue)
                {
                    blockId = slopeBlockId.Value;
                    return;
                }

                placeable = false;
            }

            #endregion
        }
    }
}
