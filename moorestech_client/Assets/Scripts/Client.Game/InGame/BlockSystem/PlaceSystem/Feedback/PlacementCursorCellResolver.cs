using System;
using System.Collections.Generic;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     ドラッグ列からカーソル下のセルを選ぶ。空なら-1、それ以外は必ず列内のどれかを返す
    ///     Picks the cell under the cursor from a drag; returns -1 only for an empty run, otherwise always some cell of it
    ///     完全一致が無いときの引き方はPlacementCursorMatchで呼び出し側が指定する
    ///     How to pick when no cell matches exactly is stated by the caller through PlacementCursorMatch
    /// </summary>
    internal static class PlacementCursorCellResolver
    {
        public static int Resolve(IReadOnlyList<PlaceInfo> placeInfos, Vector3Int cursorCell, PlacementCursorMatch match)
        {
            if (placeInfos.Count == 0) return -1;

            for (var i = 0; i < placeInfos.Count; i++)
            {
                if (placeInfos[i].Position == cursorCell) return i;
            }

            switch (match)
            {
                case PlacementCursorMatch.ExactCellOrLast:
                    return placeInfos.Count - 1;
                case PlacementCursorMatch.HorizontalCellOrLast:
                    // 列のYがカーソルと揃わない設置系では、まずXZ一致でカーソル直下のセルを拾う
                    // Where the run's heights differ from the cursor's, the cell under the cursor is found by XZ first
                    var horizontalIndex = ResolveByHorizontalPosition();
                    return 0 <= horizontalIndex ? horizontalIndex : placeInfos.Count - 1;
                default:
                    throw new ArgumentOutOfRangeException(nameof(match), match, null);
            }

            #region Internal

            int ResolveByHorizontalPosition()
            {
                for (var i = 0; i < placeInfos.Count; i++)
                {
                    var position = placeInfos[i].Position;
                    if (position.x == cursorCell.x && position.z == cursorCell.z) return i;
                }
                return -1;
            }

            #endregion
        }
    }
}
