using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     既存ブロックの入出力辺と坂の真上の書き込み先
    ///     Sink for existing blocks' port edges and slope heads
    /// </summary>
    public interface IBeltRouteEdgeSink
    {
        void AddOutputEdge(BeltRouteEdge edge);
        void AddInputEdge(BeltRouteEdge edge);
        void MarkSlopeHead(Vector3Int cell);
    }
}
