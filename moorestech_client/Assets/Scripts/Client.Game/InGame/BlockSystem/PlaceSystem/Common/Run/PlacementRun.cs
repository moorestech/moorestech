using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run
{
    /// <summary>
    ///     ドラッグ列の生成結果。セル列・不可原因列・伸長軸を1値で持つ
    ///     One drag run: its cells, the per-cell block causes and the extended axis
    /// </summary>
    public class PlacementRun
    {
        // 不可原因列はCellsと同じ添字で並走する
        // The block cause column runs alongside Cells on the same index
        public List<PlaceInfo> Cells { get; }
        public List<PlacementBlockCause> BlockCauses { get; }
        public PlacementRunAxis Axis { get; }

        // カーソル下セルの添字。地形追従でYが動くため位置一致では引き当てられない
        // Index of the cell under the cursor; terrain following moves Y so it cannot be found by position
        public int CursorIndex { get; }

        public PlacementRun(List<PlaceInfo> cells, List<PlacementBlockCause> blockCauses, PlacementRunAxis axis, int cursorIndex)
        {
            Cells = cells;
            BlockCauses = blockCauses;
            Axis = axis;
            CursorIndex = cursorIndex;
        }
    }
}
