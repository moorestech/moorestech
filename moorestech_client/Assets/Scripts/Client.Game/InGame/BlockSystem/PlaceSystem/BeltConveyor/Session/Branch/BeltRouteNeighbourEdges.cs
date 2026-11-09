using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    /// <summary>
    ///     分岐判定が隣のブロックの入出力辺を写す入れ物。辺の有無だけを答える
    ///     Holds the neighbouring blocks' port edges for the branch judge and answers only whether an edge exists
    /// </summary>
    public sealed class BeltRouteNeighbourEdges : IBeltRouteEdgeSink
    {
        private readonly HashSet<BeltRouteEdge> _outputEdges = new();
        private readonly HashSet<BeltRouteEdge> _inputEdges = new();

        public void AddOutputEdge(BeltRouteEdge edge) => _outputEdges.Add(edge);
        public void AddInputEdge(BeltRouteEdge edge) => _inputEdges.Add(edge);

        // 分岐の向きと出口は辺だけで決まる。坂の真上は判定に使わないので受け流す
        // The branch facing and exits follow from edges alone; slope heads play no part, so they are ignored
        public void MarkSlopeHead(Vector3Int cell)
        {
        }

        public bool HasOutputEdge(BeltRouteEdge edge) => _outputEdges.Contains(edge);
        public bool HasInputEdge(BeltRouteEdge edge) => _inputEdges.Contains(edge);
    }
}
