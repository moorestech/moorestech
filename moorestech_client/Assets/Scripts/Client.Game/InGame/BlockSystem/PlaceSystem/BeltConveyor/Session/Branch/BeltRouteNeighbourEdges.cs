using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session.Branch
{
    /// <summary>
    ///     分岐判定用に隣の入出力辺を写す入れ物
    ///     Holds neighbouring port edges for the branch judge
    /// </summary>
    public sealed class BeltRouteNeighbourEdges : IBeltRouteEdgeSink
    {
        private readonly HashSet<BeltRouteEdge> _outputEdges = new();
        private readonly HashSet<BeltRouteEdge> _inputEdges = new();

        public void AddOutputEdge(BeltRouteEdge edge) => _outputEdges.Add(edge);
        public void AddInputEdge(BeltRouteEdge edge) => _inputEdges.Add(edge);

        // 坂の真上は分岐判定に不要なので無視
        // Slope heads are unused by the branch judge, so ignored
        public void MarkSlopeHead(Vector3Int cell)
        {
        }

        public bool HasOutputEdge(BeltRouteEdge edge) => _outputEdges.Contains(edge);
        public bool HasInputEdge(BeltRouteEdge edge) => _inputEdges.Contains(edge);
    }
}
