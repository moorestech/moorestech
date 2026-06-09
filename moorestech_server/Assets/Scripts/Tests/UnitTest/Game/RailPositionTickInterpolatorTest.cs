using System;
using System.Collections.Generic;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public sealed class RailPositionTickInterpolatorTest
    {
        [Test]
        public void TryInterpolateByWeight_WhenLinearPath_UsesCurrentWeightDistance()
        {
            // 線形経路では a:b の current 側比率だけ head が進む。
            // On a linear route, the head advances by the current-side ratio.
            var provider = new TestRailGraphProvider();
            provider.AddNode(0);
            provider.AddNode(1);
            provider.AddNode(2);
            provider.AddEdge(1, 0, 10);
            provider.AddEdge(0, 2, 10);

            var previous = CreateRailPosition(provider, new[] { 0, 1 }, 5, 0);
            var current = CreateRailPosition(provider, new[] { 2, 0, 1 }, 5, 0);

            var success = RailPositionTickInterpolator.TryInterpolateByWeight(
                previous,
                current,
                provider,
                provider,
                1,
                1,
                out var interpolated);

            Assert.IsTrue(success);
            Assert.AreEqual(5, interpolated.TrainLength);
            Assert.AreEqual(2, interpolated.GetNodeApproaching().NodeId);
            Assert.AreEqual(5, interpolated.GetDistanceToNextNode());
        }

        [Test]
        public void TryInterpolateByWeight_WhenPreviousGraphEdgeChanged_UsesCurrentShortestPath()
        {
            // 旧 graph の node を現在 graph に解決し直し、node 間を最短経路でつなぐ。
            // Re-resolve old graph nodes on the current graph and reconnect them by shortest paths.
            var oldProvider = new TestRailGraphProvider();
            oldProvider.AddNode(0);
            oldProvider.AddNode(1);
            oldProvider.AddEdge(1, 0, 10);

            var currentProvider = new TestRailGraphProvider();
            currentProvider.AddNode(0);
            currentProvider.AddNode(1);
            currentProvider.AddNode(2);
            currentProvider.AddNode(3);
            currentProvider.AddEdge(1, 3, 4);
            currentProvider.AddEdge(3, 0, 6);
            currentProvider.AddEdge(0, 2, 10);

            var previous = CreateRailPosition(oldProvider, new[] { 0, 1 }, 5, 0);
            var current = CreateRailPosition(currentProvider, new[] { 2, 0, 3, 1 }, 5, 0);

            var success = RailPositionTickInterpolator.TryInterpolateByWeight(
                previous,
                current,
                currentProvider,
                currentProvider,
                1,
                1,
                out var interpolated);

            Assert.IsTrue(success);
            Assert.AreEqual(2, interpolated.GetNodeApproaching().NodeId);
            Assert.AreEqual(5, interpolated.GetDistanceToNextNode());
        }

        [Test]
        public void TryInterpolateByWeight_WhenPreviousNodeIsMissing_ReturnsFalse()
        {
            // 旧 RailPosition の node が1つでも現在 graph で解決できない場合は補間しない。
            // Interpolation fails when any old RailPosition node is missing on the current graph.
            var oldProvider = new TestRailGraphProvider();
            oldProvider.AddNode(0);
            oldProvider.AddNode(1);
            oldProvider.AddEdge(1, 0, 10);

            var currentProvider = new TestRailGraphProvider();
            currentProvider.AddNode(0);
            currentProvider.AddNode(2);
            currentProvider.AddEdge(0, 2, 10);

            var previous = CreateRailPosition(oldProvider, new[] { 0, 1 }, 1, 0);
            var current = CreateRailPosition(currentProvider, new[] { 2, 0 }, 1, 0);

            var success = RailPositionTickInterpolator.TryInterpolateByWeight(
                previous,
                current,
                currentProvider,
                currentProvider,
                1,
                1,
                out _);

            Assert.IsFalse(success);
        }

        [Test]
        public void TryInterpolateByWeight_WhenTargetIsBehindOnSameSegment_UsesShortestDetour()
        {
            // 同一セグメント上で target が後ろにある場合は、最短 detour を通る。
            // When the target is behind on the same segment, the shortest detour is used.
            var provider = new TestRailGraphProvider();
            provider.AddNode(0);
            provider.AddNode(1);
            provider.AddNode(2);
            provider.AddEdge(1, 0, 10);
            provider.AddEdge(0, 2, 2);
            provider.AddEdge(2, 1, 2);

            var previous = CreateRailPosition(provider, new[] { 0, 1 }, 0, 3);
            var current = CreateRailPosition(provider, new[] { 0, 1 }, 0, 7);

            var success = RailPositionTickInterpolator.TryInterpolateByWeight(
                previous,
                current,
                provider,
                provider,
                1,
                1,
                out var interpolated);

            Assert.IsTrue(success);
            Assert.AreEqual(2, interpolated.GetNodeApproaching().NodeId);
            Assert.AreEqual(0, interpolated.GetDistanceToNextNode());
        }

        private static RailPosition CreateRailPosition(TestRailGraphProvider provider, IReadOnlyList<int> nodeIds, int trainLength, int distanceToNextNode)
        {
            // テスト用 node id 列を RailPosition の node 列へ変換する。
            // Convert test node ids into a RailPosition node chain.
            var nodes = new List<IRailNode>(nodeIds.Count);
            for (var i = 0; i < nodeIds.Count; i++)
            {
                Assert.IsTrue(provider.TryGetNode(nodeIds[i], out var node));
                nodes.Add(node);
            }
            return new RailPosition(nodes, trainLength, distanceToNextNode);
        }

        private sealed class TestRailGraphProvider : IRailGraphProvider, IRailGraphTraversalProvider
        {
            private readonly Dictionary<int, TestRailNode> _nodes = new();
            private readonly Dictionary<ConnectionDestination, int> _destinationToNodeId = new();
            private readonly List<List<(int targetId, int distance)>> _connectNodes = new();
            private readonly RailGraphPathFinder _pathFinder = new();

            public IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> ConnectNodes => _connectNodes;

            public void AddNode(int nodeId)
            {
                EnsureNodeSlot(nodeId);
                var node = new TestRailNode(nodeId, this);
                _nodes[nodeId] = node;
                _destinationToNodeId[node.GetConnectionDestination()] = nodeId;
            }

            public void AddEdge(int fromNodeId, int toNodeId, int distance)
            {
                EnsureNodeSlot(fromNodeId);
                EnsureNodeSlot(toNodeId);
                _connectNodes[fromNodeId].Add((toNodeId, distance));
            }

            public bool TryGetNode(int nodeId, out IRailNode node)
            {
                if (_nodes.TryGetValue(nodeId, out var testNode))
                {
                    node = testNode;
                    return true;
                }
                node = null;
                return false;
            }

            public IRailNode ResolveRailNode(ConnectionDestination destination)
            {
                if (!_destinationToNodeId.TryGetValue(destination, out var nodeId))
                {
                    return null;
                }
                return _nodes.TryGetValue(nodeId, out var node) ? node : null;
            }

            public IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end)
            {
                if (start == null || end == null)
                {
                    return Array.Empty<IRailNode>();
                }

                // shared path finder を使い、テスト provider でも本番と同じ最短経路を返す。
                // Use the shared path finder so tests follow production shortest-path behavior.
                var pathIds = _pathFinder.FindShortestPath(_connectNodes, start.NodeId, end.NodeId);
                var nodes = new List<IRailNode>(pathIds.Count);
                for (var i = 0; i < pathIds.Count; i++)
                {
                    if (!TryGetNode(pathIds[i], out var node))
                    {
                        return Array.Empty<IRailNode>();
                    }
                    nodes.Add(node);
                }
                return nodes;
            }

            public int GetDistance(IRailNode start, IRailNode end, bool useFindPath)
            {
                if (start == null || end == null)
                {
                    return -1;
                }
                if (!useFindPath)
                {
                    return FindDirectDistance(start.NodeId, end.NodeId);
                }

                var path = FindShortestPath(start, end);
                return RailNodeCalculate.CalculateTotalDistanceF(path);
            }

            private int FindDirectDistance(int startNodeId, int endNodeId)
            {
                if (startNodeId < 0 || startNodeId >= _connectNodes.Count)
                {
                    return -1;
                }

                // 隣接 edge だけを見て直接距離を返す。
                // Return only a direct adjacency distance.
                var edges = _connectNodes[startNodeId];
                for (var i = 0; i < edges.Count; i++)
                {
                    if (edges[i].targetId == endNodeId)
                    {
                        return edges[i].distance;
                    }
                }
                return -1;
            }

            private void EnsureNodeSlot(int nodeId)
            {
                while (_connectNodes.Count <= nodeId)
                {
                    _connectNodes.Add(new List<(int targetId, int distance)>());
                }
            }
        }

        private sealed class TestRailNode : IRailNode
        {
            private readonly TestRailGraphProvider _provider;
            private readonly ConnectionDestination _connectionDestination;

            public TestRailNode(int nodeId, TestRailGraphProvider provider)
            {
                NodeId = nodeId;
                _provider = provider;
                NodeGuid = Guid.NewGuid();
                _connectionDestination = new ConnectionDestination(new SerializableVector3Int(nodeId, 0, 0), 0, true);
                StationRef = new StationReference();
                FrontControlPoint = new RailControlPoint(Vector3.zero, Vector3.zero);
                BackControlPoint = new RailControlPoint(Vector3.zero, Vector3.zero);
            }

            public int NodeId { get; }
            public int OppositeNodeId => NodeId;
            public IRailNode OppositeNode => this;
            public ConnectionDestination ConnectionDestination => _connectionDestination;
            public Guid NodeGuid { get; }
            public IRailGraphProvider GraphProvider => _provider;
            public StationReference StationRef { get; }
            public RailControlPoint FrontControlPoint { get; }
            public RailControlPoint BackControlPoint { get; }

            public IEnumerable<IRailNode> ConnectedNodes
            {
                get
                {
                    var edges = _provider.ConnectNodes[NodeId];
                    for (var i = 0; i < edges.Count; i++)
                    {
                        if (_provider.TryGetNode(edges[i].targetId, out var node))
                        {
                            yield return node;
                        }
                    }
                }
            }

            public IEnumerable<(IRailNode node, int distance)> ConnectedNodesWithDistance
            {
                get
                {
                    var edges = _provider.ConnectNodes[NodeId];
                    for (var i = 0; i < edges.Count; i++)
                    {
                        if (_provider.TryGetNode(edges[i].targetId, out var node))
                        {
                            yield return (node, edges[i].distance);
                        }
                    }
                }
            }

            public ConnectionDestination GetConnectionDestination()
            {
                return _connectionDestination;
            }

            public int GetDistanceToNode(IRailNode node, bool useFindPath)
            {
                return _provider.GetDistance(this, node, useFindPath);
            }
        }
    }
}
