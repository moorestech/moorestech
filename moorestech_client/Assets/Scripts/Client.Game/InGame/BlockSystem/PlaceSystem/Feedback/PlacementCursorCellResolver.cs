using System.Collections.Generic;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     ドラッグ列からカーソル下のセルを選ぶ。一致が無ければ末尾セル（ElectricWireAutoConnectPreviewと同じ規則）、空なら-1
    ///     Picks the cell under the cursor from a drag; falls back to the last cell (same rule as ElectricWireAutoConnectPreview), -1 when empty
    ///     張替え列だけは既設の高さへ追従しno-opセルも落とすため、XZ一致で引き末尾フォールバックはしない
    ///     A replace run alone follows the existing heights and drops no-op cells, so it matches on XZ and never falls back to the last cell
    /// </summary>
    internal static class PlacementCursorCellResolver
    {
        public static int Resolve(IReadOnlyList<PlaceInfo> placeInfos, Vector3Int cursorCell)
        {
            if (placeInfos.Count == 0) return -1;

            for (var i = 0; i < placeInfos.Count; i++)
            {
                if (placeInfos[i].Position == cursorCell) return i;
            }

            // 末尾フォールバックは無関係なセルの理由をカーソルの理由として出すため、張替え列では使わない
            // The last-cell fallback would report an unrelated cell's reason as the cursor's, so a replace run refuses it
            if (ContainsReplaceCell()) return ResolveByHorizontalPosition();

            return placeInfos.Count - 1;

            #region Internal

            bool ContainsReplaceCell()
            {
                for (var i = 0; i < placeInfos.Count; i++)
                {
                    if (placeInfos[i].IsReplace) return true;
                }
                return false;
            }

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
