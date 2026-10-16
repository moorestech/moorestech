using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete.Box
{
    /// <summary>
    ///     箱選択の「箱に入る」判定（点・曲線サンプル）。セル [min, max] を含む直方体に対する純関数。ブロックは BlueprintCopyTargetRule の箱と IsOverlap を使う
    ///     Box-selection membership for points and curve samples over the inclusive cell box [min, max]; blocks use BlueprintCopyTargetRule's box with IsOverlap
    /// </summary>
    public static class DeleteBoxRule
    {
        // min の面は含み、max セルの外側面（max+1）は含まない
        // Inclusive at the min faces, exclusive at the far faces of the max cell (max+1)
        public static bool IsPointInBox(Vector3 point, Vector3Int min, Vector3Int max)
        {
            return min.x <= point.x && point.x < max.x + 1
                   && min.y <= point.y && point.y < max.y + 1
                   && min.z <= point.z && point.z < max.z + 1;
        }

        public static bool IsAnyPointInBox(IReadOnlyList<Vector3> points, Vector3Int min, Vector3Int max)
        {
            foreach (var point in points)
            {
                if (IsPointInBox(point, min, max)) return true;
            }
            return false;
        }
    }
}
