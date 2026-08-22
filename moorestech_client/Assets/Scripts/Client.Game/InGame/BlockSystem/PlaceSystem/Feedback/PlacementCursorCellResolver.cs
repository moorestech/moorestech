using System.Collections.Generic;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Feedback
{
    /// <summary>
    ///     ドラッグ列からカーソル下のセルを選ぶ。一致が無ければ末尾セル（ElectricWireAutoConnectPreviewと同じ規則）、空なら-1
    ///     Picks the cell under the cursor from a drag; falls back to the last cell (same rule as ElectricWireAutoConnectPreview), -1 when empty
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

            return placeInfos.Count - 1;
        }
    }
}
