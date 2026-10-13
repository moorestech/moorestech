using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Topology;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport.View
{
    // 出口までの距離とsegmentの経路から「今のマス・進行量」を求め、進入方向と合わせてワールド座標にする。マス中心からマス中心へ直線で補間し、坂も同じ進行量で補間する
    // Derives the current cell and progress from the distance to exit and the segment path, then combines them with the entry direction into a world position; interpolates in a straight line between cell centers, slopes included
    // 進行量は0で搬入元マスの中心、256で今のマスの中心。先頭が出口に達したとき、アイテムの中心は末尾マスの中心にある
    // Progress 0 is the source cell's center and 256 the current cell's center; when the head reaches the exit, the item's center is at the last cell's center
    public static class BeltItemWorldPositionCalculator
    {
        // ベルト面の高さ(ブロック底からアイテム中心まで)。旧Bezier経路の既定値に合わせる
        // Belt surface height (block base to item center), matching the old Bezier path's default
        private const float SurfaceHeight = 0.35f;
        // 坂マスは両端が1段違うので中心が半段上がる
        // A slope cell's ends differ by one step, so its center sits half a step higher
        private const float SlopeCenterRise = 0.5f;
        private const int CellLength = BeltConstants.ItemWidth;
        private const int HalfCell = CellLength / 2;

        public static Vector3 Calculate(BeltSegmentShape[] shapes, int segmentIndex, int distanceToExit, BeltEntryDirection entryDirection)
        {
            var shape = shapes[segmentIndex];
            var cells = shape.Cells;
            // 先頭の入口からの走行距離。アイテム中心はその半マス後ろなので、今のマス番号と進行量は走行距離そのものから出る
            // The head's travel from the entry; the item center is half a cell behind, so the current cell index and progress fall out of the travel itself
            var travel = cells.Length * CellLength - distanceToExit;
            var currentCellIndex = travel / CellLength;
            var progress = travel - currentCellIndex * CellLength;
            // 先頭が出口に達した状態は「末尾マスの中心に到達」。末尾マスを今のマスとして進行量を一杯にする
            // The head at the exit means "arrived at the last cell's center"; keep the last cell current with full progress
            if (currentCellIndex == cells.Length)
            {
                currentCellIndex--;
                progress = CellLength;
            }

            var source = currentCellIndex == 0 ? SourceCenterBeforeFirstCell() : CellCenter(shape, currentCellIndex - 1);
            var target = CellCenter(shape, currentCellIndex);
            return Vector3.Lerp(source, target, progress / (float)CellLength);

            #region Internal

            // 1マスめの搬入元。進入方向と一致する入力がベルトのsegmentならその末尾マスの中心、それ以外(機械・経路に無い旧方向)は進入方向の隣マスの平らな中心
            // The source of the first cell: the input matching the entry direction when it is a belt segment gives its last cell's center; otherwise (machine, or an old direction absent from the path) the flat center of the neighbour in the entry direction
            Vector3 SourceCenterBeforeFirstCell()
            {
                foreach (var input in shape.Inputs)
                {
                    if (input.EntryDirection != entryDirection || !HasCells(input)) continue;
                    var partner = shapes[input.PartnerSegmentIndex];
                    return CellCenter(partner, partner.Cells.Length - 1);
                }
                return FlatCenter(cells[0].Position + EntryOffset(entryDirection));
            }

            bool HasCells(BeltLinkShape link)
            {
                return !link.IsMachine && shapes[link.PartnerSegmentIndex].Cells.Length != 0;
            }

            #endregion
        }

        // マスの中心。坂マスは半段上がる
        // The cell's center; a slope cell sits half a step higher
        private static Vector3 CellCenter(BeltSegmentShape shape, int cellIndex)
        {
            var center = FlatCenter(shape.Cells[cellIndex].Position);
            return IsSlope(shape, cellIndex) ? center + new Vector3(0f, SlopeCenterRise, 0f) : center;
        }

        // 坂マスか。隣のマスが1段上なら坂(上り坂は先が、下り坂は手前が1段上)。段差はsegment内ならマスの座標、境界なら接続の進入方向から読む
        // Whether the cell is a slope: a neighbour one step up means a slope (ahead for an up slope, behind for a down slope); read from cell positions inside the segment, from link entry directions at its ends
        private static bool IsSlope(BeltSegmentShape shape, int cellIndex)
        {
            var cells = shape.Cells;
            var y = cells[cellIndex].Position.y;
            if (0 < cellIndex && cells[cellIndex - 1].Position.y == y + 1) return true;
            if (cellIndex < cells.Length - 1 && cells[cellIndex + 1].Position.y == y + 1) return true;
            if (cellIndex == 0 && AnyInputFromAbove()) return true;
            return cellIndex == cells.Length - 1 && AnyOutputFromBelow();

            #region Internal

            bool AnyInputFromAbove()
            {
                foreach (var input in shape.Inputs)
                {
                    if (IsFromAbove(input.EntryDirection)) return true;
                }
                return false;
            }

            bool AnyOutputFromBelow()
            {
                foreach (var output in shape.Outputs)
                {
                    if (IsFromBelow(output.EntryDirection)) return true;
                }
                return false;
            }

            #endregion
        }

        private static Vector3 FlatCenter(Vector3Int cellPosition)
        {
            return new Vector3(cellPosition.x + 0.5f, cellPosition.y + SurfaceHeight, cellPosition.z + 0.5f);
        }

        // 今のマスから見た搬入元マスへの差分。上から来たなら+1段、下から来たなら-1段
        // Offset from the current cell to the source cell; one step up when it came from above, one step down when from below
        private static Vector3Int EntryOffset(BeltEntryDirection entryDirection)
        {
            var offset = BeltTopologyGeometry.DirectionOffset(BeltEntryDirections.Horizontal(entryDirection));
            if (IsFromAbove(entryDirection)) return offset + Vector3Int.up;
            if (IsFromBelow(entryDirection)) return offset + Vector3Int.down;
            return offset;
        }

        // 高さ段の判定はCoreの符号化(BeltEntryDirections)に任せ、こちらで値の並びを解釈しない
        // Height-step checks defer to Core's encoding (BeltEntryDirections) instead of interpreting the value layout here
        private static bool IsFromAbove(BeltEntryDirection entryDirection)
        {
            return BeltEntryDirections.FromAbove(BeltEntryDirections.Horizontal(entryDirection)) == entryDirection;
        }

        private static bool IsFromBelow(BeltEntryDirection entryDirection)
        {
            return BeltEntryDirections.FromBelow(BeltEntryDirections.Horizontal(entryDirection)) == entryDirection;
        }
    }
}
