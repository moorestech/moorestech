using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World
{
    /// <summary>
    ///     既存ブロックの入出力辺と坂の真上の書き込み先。本番の世界・分岐判定の辺の入れ物・テストの箱が実装する
    ///     Where existing blocks' port edges and slope heads are written; implemented by the live world, the branch judge's edge holder and the test box
    /// </summary>
    public interface IBeltRouteEdgeSink
    {
        void AddOutputEdge(BeltRouteEdge edge);
        void AddInputEdge(BeltRouteEdge edge);
        void MarkSlopeHead(Vector3Int cell);
    }
}
