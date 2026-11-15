using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run
{
    /// <summary>
    ///     押下時に凍結するドラッグ開始状態
    ///     The drag start state frozen at the press
    ///     開始セル・面種別・高さを1値に畳み、片方だけ残る中途半端な状態を作れなくする
    ///     Folding the start cell, surface kind and height into one value makes a half-cleared state unrepresentable
    /// </summary>
    public class PlacementDragSession
    {
        public Vector3Int StartCell { get; }
        public PlacementHitSurfaceKind SurfaceKind { get; }
        public int StartHeightOffset { get; }

        public PlacementDragSession(Vector3Int startCell, PlacementHitSurfaceKind surfaceKind, int startHeightOffset)
        {
            StartCell = startCell;
            SurfaceKind = surfaceKind;
            StartHeightOffset = startHeightOffset;
        }
    }
}
