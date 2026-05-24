using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.SaveLoad;
using Game.Train.Unit;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class TrainUnitBranchSelectorTest
    {
        [Test]
        public void SelectManualBranchNode_ChoosesVisualLeftStraightRight()
        {
            var justPassed = CreateNode(100, new Vector3(0f, 0f, -10f), Vector3.forward, Vector3.back);
            var junction = CreateNode(200, Vector3.zero, Vector3.forward, Vector3.back);

            // NodeId順ではなく、進行方向から見た左右順で A/無操作/D を決める。
            // Decide A/neutral/D by visual left-to-right order from travel direction, not NodeId order.
            var right = CreateCandidate(1, new Vector3(10f, 0f, 10f), junction);
            var left = CreateCandidate(50, new Vector3(-10f, 0f, 10f), junction);
            var straight = CreateCandidate(20, new Vector3(0f, 0f, 10f), junction);
            var candidates = new IRailNode[] { right, left, straight };

            Assert.AreSame(left, TrainUnitBranchSelector.SelectManualBranchNode(justPassed, junction, candidates, TrainUnitBranchCommand.Previous));
            Assert.AreSame(straight, TrainUnitBranchSelector.SelectManualBranchNode(justPassed, junction, candidates, TrainUnitBranchCommand.Neutral));
            Assert.AreSame(right, TrainUnitBranchSelector.SelectManualBranchNode(justPassed, junction, candidates, TrainUnitBranchCommand.Next));
        }

        [Test]
        public void SelectManualBranchNode_OrdersVerticalOverlapByHeightThenDestination()
        {
            var justPassed = CreateNode(100, new Vector3(0f, 0f, -10f), Vector3.forward, Vector3.back);
            var junction = CreateNode(200, Vector3.zero, Vector3.forward, Vector3.back);

            // 左右角が完全に重なる場合は高さで順序を固定する。
            // When horizontal angles fully overlap, stabilize the order by height.
            var upper = CreateCandidate(2, new Vector3(0f, 3f, 10f), junction);
            var lower = CreateCandidate(1, new Vector3(0f, -2f, 10f), junction);
            var candidates = new IRailNode[] { upper, lower };

            Assert.AreSame(lower, TrainUnitBranchSelector.SelectManualBranchNode(justPassed, junction, candidates, TrainUnitBranchCommand.Previous));
            Assert.AreSame(lower, TrainUnitBranchSelector.SelectManualBranchNode(justPassed, junction, candidates, TrainUnitBranchCommand.Neutral));
            Assert.AreSame(upper, TrainUnitBranchSelector.SelectManualBranchNode(justPassed, junction, candidates, TrainUnitBranchCommand.Next));
        }

        private static TestRailNode CreateCandidate(int nodeId, Vector3 position, IRailNode junction)
        {
            var backDirection = (junction.FrontControlPoint.OriginalPosition - position).normalized;
            return CreateNode(nodeId, position, Vector3.forward, backDirection);
        }

        private static TestRailNode CreateNode(int nodeId, Vector3 position, Vector3 frontDirection, Vector3 backDirection)
        {
            var destination = new ConnectionDestination(new Vector3Int(nodeId, Mathf.RoundToInt(position.y), Mathf.RoundToInt(position.z)), nodeId, true);
            return new TestRailNode(nodeId, destination, new RailControlPoint(position, frontDirection), new RailControlPoint(position, backDirection));
        }

        private sealed class TestRailNode : IRailNode
        {
            private readonly int _nodeId;
            private readonly ConnectionDestination _connectionDestination;
            private readonly RailControlPoint _frontControlPoint;
            private readonly RailControlPoint _backControlPoint;
            private readonly StationReference _stationReference;

            public TestRailNode(int nodeId, ConnectionDestination connectionDestination, RailControlPoint frontControlPoint, RailControlPoint backControlPoint)
            {
                _nodeId = nodeId;
                _connectionDestination = connectionDestination;
                _frontControlPoint = frontControlPoint;
                _backControlPoint = backControlPoint;
                _stationReference = new StationReference();
            }

            public int NodeId => _nodeId;
            public int OppositeNodeId => _nodeId ^ 1;
            public IRailNode OppositeNode => null;
            public ConnectionDestination ConnectionDestination => _connectionDestination;
            public Guid NodeGuid => Guid.Empty;
            public IRailGraphProvider GraphProvider => null;
            public StationReference StationRef => _stationReference;
            public RailControlPoint FrontControlPoint => _frontControlPoint;
            public RailControlPoint BackControlPoint => _backControlPoint;
            public IEnumerable<IRailNode> ConnectedNodes => Array.Empty<IRailNode>();
            public IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance => Array.Empty<(IRailNode node, int distance)>();

            public int GetDistanceToNode(IRailNode node, bool useFindPath)
            {
                return 0;
            }
        }
    }
}
