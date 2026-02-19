using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    public class RailPathTracerForwardDfsTest
    {
        [Test]
        public void TryTraceForwardRoutesByDfs_WhenDistanceWithinStartSegment_ReturnsSingleLocalRoute()
        {
            // 日本語: 分岐に到達しない距離は開始セグメント内の1経路のみを返す。
            // English: Distance within the start segment should return one local route.
            var provider = BuildProviderWithLinearStartSegment(10);
            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 7, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 4, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(4, routes[0].TrainLength);
            Assert.AreEqual(3, routes[0].GetDistanceToNextNode());
            CollectionAssert.AreEqual(new[] { 0, 1 }, GetNodeIds(routes[0]));
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenDistanceEqualsDistanceToApproaching_ReturnsSingleRouteAtApproaching()
        {
            // 日本語: 距離がdistanceToApproachingと一致する場合はapproachingノード終端になる。
            // English: Distance exactly matching distanceToApproaching should terminate at approaching node.
            var provider = BuildProviderWithLinearStartSegment(10);
            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 7, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 7, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(7, routes[0].TrainLength);
            Assert.AreEqual(0, routes[0].GetDistanceToNextNode());
            Assert.AreEqual(0, routes[0].GetNodeApproaching().NodeId);
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenBranchExists_EnumeratesAllReachableRoutes()
        {
            // 日本語: approaching以降に分岐がある場合は到達可能な経路を全列挙する。
            // English: Branches after approaching should enumerate all reachable routes.
            var provider = BuildProviderWithLinearStartSegment(10);
            provider.AddNode(2);
            provider.AddNode(3);
            provider.AddNode(4);
            provider.AddEdge(0, 2, 4);
            provider.AddEdge(2, 4, 2);
            provider.AddEdge(0, 3, 6);

            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 2, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 8, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(2, routes.Count);
            CollectionAssert.AreEqual(new[] { 4, 2, 0, 1 }, GetNodeIds(routes[0]));
            CollectionAssert.AreEqual(new[] { 3, 0, 1 }, GetNodeIds(routes[1]));
            Assert.AreEqual(0, routes[0].GetDistanceToNextNode());
            Assert.AreEqual(0, routes[1].GetDistanceToNextNode());
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenStoppingInsideEdge_ReturnsNonZeroDistanceToNext()
        {
            // 日本語: 1辺の途中で終端する場合はdistanceToNextNodeが正値で返る。
            // English: Terminating inside an edge should return positive distanceToNextNode.
            var provider = BuildProviderWithLinearStartSegment(10);
            provider.AddNode(2);
            provider.AddEdge(0, 2, 10);

            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 3, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 9, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(4, routes[0].GetDistanceToNextNode());
            CollectionAssert.AreEqual(new[] { 2, 0, 1 }, GetNodeIds(routes[0]));
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenRemainingZero_DoesNotIncludeFurtherZeroDistanceNode()
        {
            // 日本語: 残距離0で終端した場合は、その先の0距離ノードを含めない。
            // English: When remaining distance is zero, do not include farther zero-distance nodes.
            var provider = BuildProviderWithLinearStartSegment(10);
            provider.AddNode(2);
            provider.AddEdge(0, 2, 0);

            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 2, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 2, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(1, routes.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, GetNodeIds(routes[0]));
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenZeroDistanceEdgeBeforePositiveEdge_TraversesAndFindsRoute()
        {
            // 日本語: 0距離辺の先に正距離辺がある場合は探索を継続して到達できる。
            // English: DFS should traverse zero-distance edges before consuming positive edges.
            var provider = BuildProviderWithLinearStartSegment(1);
            provider.AddNode(2);
            provider.AddNode(3);
            provider.AddEdge(0, 2, 0);
            provider.AddEdge(2, 3, 2);

            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 1, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 3, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(1, routes.Count);
            CollectionAssert.AreEqual(new[] { 3, 2, 0, 1 }, GetNodeIds(routes[0]));
            Assert.AreEqual(0, routes[0].GetDistanceToNextNode());
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenZeroDistanceCycleExists_DoesNotLoopInfinitely()
        {
            // 日本語: 0距離サイクルがあっても状態ガードで無限再帰せず列挙できる。
            // English: State guard should prevent infinite recursion on zero-distance cycles.
            var provider = BuildProviderWithLinearStartSegment(1);
            provider.AddNode(2);
            provider.AddNode(3);
            provider.AddEdge(0, 2, 0);
            provider.AddEdge(2, 0, 0);
            provider.AddEdge(2, 3, 2);

            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 1, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 3, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(1, routes.Count);
            CollectionAssert.AreEqual(new[] { 3, 2, 0, 1 }, GetNodeIds(routes[0]));
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenStartIsOnNode_EnumeratesOnlyOutgoingDirections()
        {
            // 日本語: 開始点がノード直上ならそのノードのoutgoing辺のみで全列挙する。
            // English: On-node start should enumerate only outgoing directions.
            var provider = new TestTraversalProvider();
            provider.AddNode(0);
            provider.AddNode(1);
            provider.AddNode(2);
            provider.AddEdge(0, 1, 3);
            provider.AddEdge(0, 2, 3);

            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, null, 0, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 3, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(2, routes.Count);
            CollectionAssert.AreEqual(new[] { 1, 0 }, GetNodeIds(routes[0]));
            CollectionAssert.AreEqual(new[] { 2, 0 }, GetNodeIds(routes[1]));
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenDistanceIsNegative_ReturnsFalse()
        {
            // 日本語: 負の距離は無効入力としてfalseを返す。
            // English: Negative distance should be rejected as invalid input.
            var provider = BuildProviderWithLinearStartSegment(10);
            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 7, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, -1, out var routes);

            Assert.IsFalse(success);
            Assert.AreEqual(0, routes.Count);
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenApproachingNodeIsMissingInProvider_ReturnsFalse()
        {
            // 日本語: 開始点ノードがプロバイダから消えた場合は探索不能としてfalseを返す。
            // English: Missing approaching node in provider should fail safely.
            var provider = BuildProviderWithLinearStartSegment(10);
            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 7, 0);
            provider.RemoveNode(0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 4, out var routes);

            Assert.IsFalse(success);
            Assert.AreEqual(0, routes.Count);
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenStartLengthIsNonZero_NormalizesToHeadPoint()
        {
            // 日本語: 入力lengthが0でなくても先頭点へ正規化して同じ結果を返す。
            // English: Non-zero input length should be normalized to head point.
            var provider = BuildProviderWithLinearStartSegment(10);
            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 7, 2);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 4, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(4, routes[0].TrainLength);
            Assert.AreEqual(3, routes[0].GetDistanceToNextNode());
            CollectionAssert.AreEqual(new[] { 0, 1 }, GetNodeIds(routes[0]));
        }

        [Test]
        public void TryTraceForwardRoutesByDfs_WhenBranchTargetsDiffer_ReturnsDeterministicOrder()
        {
            // 日本語: 分岐列挙順はtargetId昇順で安定させる。
            // English: Branch ordering should be deterministic by ascending targetId.
            var provider = BuildProviderWithLinearStartSegment(2);
            provider.AddNode(3);
            provider.AddNode(4);
            provider.AddNode(5);
            provider.AddEdge(0, 5, 2);
            provider.AddEdge(0, 3, 2);
            provider.AddEdge(0, 4, 2);

            var tracer = new RailPathTracer(provider);
            var start = CreateStartPoint(provider, 0, 1, 2, 0);

            var success = tracer.TryTraceForwardRoutesByDfs(start, 4, out var routes);

            Assert.IsTrue(success);
            Assert.AreEqual(3, routes.Count);
            Assert.AreEqual(3, routes[0].GetNodeApproaching().NodeId);
            Assert.AreEqual(4, routes[1].GetNodeApproaching().NodeId);
            Assert.AreEqual(5, routes[2].GetNodeApproaching().NodeId);
        }

        private static TestTraversalProvider BuildProviderWithLinearStartSegment(int segmentDistance)
        {
            // 日本語: 開始セグメント passed(1) -> approaching(0) を持つ最小グラフを作る。
            // English: Build minimal graph with start segment passed(1) -> approaching(0).
            var provider = new TestTraversalProvider();
            provider.AddNode(0);
            provider.AddNode(1);
            provider.AddEdge(1, 0, segmentDistance);
            return provider;
        }

        private static RailPosition CreateStartPoint(TestTraversalProvider provider, int approachingId, int? passedId, int distanceToApproaching, int trainLength)
        {
            // 日本語: テスト入力用のRailPositionを構築する。
            // English: Build a RailPosition used as test input.
            Assert.IsTrue(provider.TryGetNode(approachingId, out var approaching));
            var nodes = new List<IRailNode> { approaching };
            if (passedId.HasValue)
            {
                Assert.IsTrue(provider.TryGetNode(passedId.Value, out var passed));
                nodes.Add(passed);
            }
            return new RailPosition(nodes, trainLength, distanceToApproaching);
        }

        private static IReadOnlyList<int> GetNodeIds(RailPosition railPosition)
        {
            // 日本語: 比較しやすいようにノード列をIDへ変換する。
            // English: Convert node chain to ids for assertions.
            var nodes = railPosition.GetRailNodes();
            var ids = new List<int>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                ids.Add(nodes[i].NodeId);
            }
            return ids;
        }

        private sealed class TestTraversalProvider : IRailGraphTraversalProvider, IRailGraphProvider
        {
            private readonly Dictionary<int, TestRailNode> _nodes = new();
            private readonly Dictionary<ConnectionDestination, int> _destinationToNodeId = new();
            private readonly List<List<(int targetId, int distance)>> _connectNodes = new();

            public IReadOnlyList<IReadOnlyList<(int targetId, int distance)>> ConnectNodes => _connectNodes;

            public void AddNode(int nodeId)
            {
                EnsureNodeSlot(nodeId);
                var node = new TestRailNode(nodeId, this);
                _nodes[nodeId] = node;
                _destinationToNodeId[node.ConnectionDestination] = nodeId;
            }

            public void RemoveNode(int nodeId)
            {
                if (_nodes.TryGetValue(nodeId, out var node))
                {
                    _destinationToNodeId.Remove(node.ConnectionDestination);
                    _nodes.Remove(nodeId);
                }

                if (nodeId >= 0 && nodeId < _connectNodes.Count)
                {
                    _connectNodes[nodeId].Clear();
                }

                for (var i = 0; i < _connectNodes.Count; i++)
                {
                    _connectNodes[i].RemoveAll(edge => edge.targetId == nodeId);
                }
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
                return _destinationToNodeId.TryGetValue(destination, out var nodeId) && _nodes.TryGetValue(nodeId, out var node)
                    ? node
                    : null;
            }

            public IReadOnlyList<IRailNode> FindShortestPath(IRailNode start, IRailNode end)
            {
                return Array.Empty<IRailNode>();
            }

            public int GetDistance(IRailNode start, IRailNode end, bool useFindPath)
            {
                if (start == null || end == null)
                {
                    return -1;
                }

                var startId = start.NodeId;
                var endId = end.NodeId;
                if (startId < 0 || startId >= _connectNodes.Count)
                {
                    return -1;
                }

                var edges = _connectNodes[startId];
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (edge.targetId == endId)
                    {
                        return edge.distance;
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
            private readonly TestTraversalProvider _provider;

            public TestRailNode(int nodeId, TestTraversalProvider provider)
            {
                NodeId = nodeId;
                _provider = provider;
                NodeGuid = Guid.NewGuid();
                ConnectionDestination = new ConnectionDestination(new SerializableVector3Int(nodeId, 0, 0), 0, true);
                StationRef = new StationReference();
                FrontControlPoint = new RailControlPoint(Vector3.zero, Vector3.zero);
                BackControlPoint = new RailControlPoint(Vector3.zero, Vector3.zero);
            }

            public int NodeId { get; }
            public int OppositeNodeId => NodeId;
            public IRailNode OppositeNode => this;
            public ConnectionDestination ConnectionDestination { get; }
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

            public int GetDistanceToNode(IRailNode node, bool useFindPath = false)
            {
                return _provider.GetDistance(this, node, useFindPath);
            }
        }
    }
}

