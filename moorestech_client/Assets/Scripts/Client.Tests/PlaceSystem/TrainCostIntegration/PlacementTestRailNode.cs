using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainCostIntegration
{
    internal sealed class PlacementTestRailNode : IRailNode
    {
        private readonly Dictionary<IRailNode, int> _distances = new();
        public int NodeId { get; }
        public int OppositeNodeId => -1;
        public IRailNode OppositeNode => null;
        public Guid NodeGuid { get; }
        public ConnectionDestination ConnectionDestination { get; }
        public IRailGraphProvider GraphProvider => null;
        public StationReference StationRef { get; }
        public RailControlPoint FrontControlPoint { get; }
        public RailControlPoint BackControlPoint { get; }
        public IEnumerable<IRailNode> ConnectedNodes => _distances.Keys;
        public IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance
        {
            get
            {
                foreach (var pair in _distances) yield return (pair.Key, pair.Value);
            }
        }

        internal PlacementTestRailNode(int id, Vector3 position)
        {
            // 既存Poseテストと同じ直線ノードで実描画計算を通す
            // Use straight nodes like the existing pose tests to exercise real preview pose calculation
            NodeId = id;
            NodeGuid = Guid.NewGuid();
            ConnectionDestination = new ConnectionDestination(new Vector3Int(id, 0, 0), 0, true);
            FrontControlPoint = new RailControlPoint(position, Vector3.forward);
            BackControlPoint = new RailControlPoint(position, Vector3.forward);
            StationRef = new StationReference();
        }

        internal void ConnectTo(IRailNode node, int distance)
        {
            _distances[node] = distance;
        }

        public int GetDistanceToNode(IRailNode node, bool useFindPath)
        {
            return _distances.TryGetValue(node, out var distance) ? distance : -1;
        }
    }
}
