using System.Collections.Generic;
using Game.Train.RailPositions;

namespace Game.Train.RailGraph
{
    public sealed class RailPathTracer
    {
        private readonly IRailGraphTraversalProvider _provider;
        private const int DefaultMaxRouteCount = 65537;

        public readonly struct UnreachedRoute
        {
            public RailPosition Route { get; }
            public int ReachedDistance { get; }

            public UnreachedRoute(RailPosition route, int reachedDistance)
            {
                Route = route;
                ReachedDistance = reachedDistance;
            }
        }

        public RailPathTracer(IRailGraphTraversalProvider provider)
        {
            _provider = provider;
        }


        public bool TryTraceForwardRoutesByDfs(RailPosition startPoint, int distance, out List<RailPosition> routes)
        {
            routes = new List<RailPosition>();
            var resultRoutes = routes;
            var isRouteLimitReached = false;

            // JP: 入力を検証し、開始点を先頭点(length=0)へ正規化する。
            // EN: Validate input and normalize to head point(length=0).
            if (startPoint == null || distance < 0)
            {
                return false;
            }

            var normalizedStart = startPoint.GetHeadRailPosition();
            var approachingNode = normalizedStart.GetNodeApproaching();
            var passedNode = normalizedStart.GetNodeJustPassed();
            var distanceToApproaching = normalizedStart.GetDistanceToNextNode();
            if (approachingNode == null)
                return false;
            if (!_provider.TryGetNode(approachingNode.NodeId, out approachingNode))
                return false;

            if (passedNode != null)
            {
                if (!_provider.TryGetNode(passedNode.NodeId, out passedNode))
                    return false;
            }
            else if (distanceToApproaching != 0)
            {
                return false;
            }

            // JP: 開始セグメント内で完結する距離は、その場で1経路だけ返す。
            // EN: Distances within the start segment return a single route immediately.
            if (distance <= distanceToApproaching)
            {
                var nodes = passedNode != null
                    ? new List<IRailNode> { approachingNode, passedNode }
                    : new List<IRailNode> { approachingNode };
                var route = new RailPosition(nodes, distance, distanceToApproaching - distance);
                resultRoutes.Add(route);
                return true;
            }

            // JP: approachingノード以降を前方向DFSで全列挙する。
            // EN: Enumerate all forward routes from the approaching node.
            var remainingDistance = distance - distanceToApproaching;
            var forwardPath = new List<int> { approachingNode.NodeId };
            var pathStateGuard = new HashSet<(int nodeId, int remain)> { (approachingNode.NodeId, remainingDistance) };
            EnumerateForward(approachingNode.NodeId, remainingDistance);
            return resultRoutes.Count > 0;

            #region Internal

            void EnumerateForward(int currentNodeId, int remain)
            {
                // JP: ルート上限に到達したら探索を即終了する。
                // EN: Stop DFS immediately once route limit is reached.
                if (isRouteLimitReached)
                {
                    return;
                }

                // JP: 残距離0は現ノード終端とし、0距離先ノードには進まない。
                // EN: Remaining zero terminates at current node and does not step into zero-length successors.
                if (remain == 0)
                {
                    if (TryCreateRouteTerminated(forwardPath, currentNodeId, 0, out var routeAtNode))
                    {
                        resultRoutes.Add(routeAtNode);
                        if (resultRoutes.Count >= DefaultMaxRouteCount )
                        {
                            isRouteLimitReached = true;
                        }
                    }
                    return;
                }

                if (currentNodeId < 0 || currentNodeId >= _provider.ConnectNodes.Count)
                {
                    return;
                }

                var outgoing = BuildSortedOutgoingEdges(currentNodeId);
                for (var i = 0; i < outgoing.Count; i++)
                {
                    var edge = outgoing[i];
                    if (edge.targetId < 0 || edge.distance < 0)
                    {
                        continue;
                    }
                    // JP: 1辺で残距離を消費できるなら、その場で経路を確定する。
                    // EN: If the remaining distance fits in one edge, emit a route immediately.
                    if (remain <= edge.distance)
                    {
                        var distanceToNextNode = edge.distance - remain;
                        // JP: ノード終端(0)と辺途中終端(>0)を同じ生成関数で扱う。
                        // EN: Build both node-end(0) and in-edge(>0) routes via one helper.
                        if (TryCreateRouteTerminated(forwardPath, edge.targetId, distanceToNextNode, out var routeOnEdge))
                        {
                            resultRoutes.Add(routeOnEdge);
                            if (resultRoutes.Count >= DefaultMaxRouteCount )
                            {
                                isRouteLimitReached = true;
                                return;
                            }
                        }
                        continue;
                    }

                    // JP: 辺を完全に消費し、次ノードへ再帰する。
                    // EN: Consume the edge and recurse to the next node.
                    var nextRemain = remain - edge.distance;
                    var nextState = (edge.targetId, nextRemain);
                    if (pathStateGuard.Contains(nextState))
                    {
                        continue;
                    }

                    forwardPath.Add(edge.targetId);
                    pathStateGuard.Add(nextState);
                    EnumerateForward(edge.targetId, nextRemain);
                    pathStateGuard.Remove(nextState);
                    forwardPath.RemoveAt(forwardPath.Count - 1);
                    if (isRouteLimitReached)
                    {
                        return;
                    }
                }
            }

            bool TryCreateRouteTerminated(IReadOnlyList<int> pathFromApproaching, int terminalApproachingNodeId, int distanceToNextNode, out RailPosition route)
            {
                route = null;
                if (pathFromApproaching == null || pathFromApproaching.Count <= 0 || terminalApproachingNodeId < 0 || distanceToNextNode < 0)
                {
                    return false;
                }

                // JP: pathFromApproachingは[approaching, ..., current]順の探索パス。
                // EN: pathFromApproaching is DFS path ordered as [approaching, ..., current].
                // JP: terminalApproachingNodeIdは終端位置の「次ノード側」を表す。
                // EN: terminalApproachingNodeId represents the "next-node side" at route end.
                // JP: remain==0終端ではcurrentNodeIdが渡され、辺途中終端ではedge.targetIdが渡される。
                // EN: remain==0 passes currentNodeId, while in-edge end passes edge.targetId.
                // JP: terminalがpath末尾と同一なら重複追加を避けるため末尾をスキップする。
                // EN: If terminal equals path tail, skip the tail to avoid duplicate nodes.
                var nodes = new List<IRailNode>();
                if (!_provider.TryGetNode(terminalApproachingNodeId, out var terminalNode))
                {
                    return false;
                }
                nodes.Add(terminalNode);

                var pathIndexStart = pathFromApproaching.Count - 1;
                if (pathFromApproaching[pathFromApproaching.Count - 1] == terminalApproachingNodeId)
                {
                    pathIndexStart = pathFromApproaching.Count - 2;
                }

                for (var i = pathIndexStart; i >= 0; i--)
                {
                    if (!_provider.TryGetNode(pathFromApproaching[i], out var node))
                    {
                        return false;
                    }
                    nodes.Add(node);
                }

                if (passedNode != null)
                {
                    nodes.Add(passedNode);
                }
                if (nodes.Count <= 0)
                {
                    return false;
                }

                route = new RailPosition(nodes, distance, distanceToNextNode);
                return true;
            }

            List<(int targetId, int distance)> BuildSortedOutgoingEdges(int nodeId)
            {
                var edges = _provider.ConnectNodes[nodeId];
                var sorted = new List<(int targetId, int distance)>(edges.Count);
                for (var i = 0; i < edges.Count; i++)
                {
                    sorted.Add(edges[i]);
                }
                sorted.Sort((left, right) =>
                    left.targetId != right.targetId
                        ? left.targetId.CompareTo(right.targetId)
                        : left.distance.CompareTo(right.distance));
                return sorted;
            }

            #endregion
        }

        // JP: 未到達経路専用。指定距離まで届かなかった経路のみを列挙する。
        // EN: Unreached-route API that enumerates only routes that cannot reach the target distance.
        public bool TryTraceForwardUnreachedRoutesByDfs(RailPosition startPoint, int distance, out List<UnreachedRoute> routes)
        {
            routes = new List<UnreachedRoute>();
            var resultRoutes = routes;
            var isRouteLimitReached = false;

            // JP: 入力を検証し、開始点を先頭点(length=0)へ正規化する。
            // EN: Validate input and normalize to head point(length=0).
            if (startPoint == null || distance < 0)
            {
                return false;
            }

            var normalizedStart = startPoint.GetHeadRailPosition();
            var approachingNode = normalizedStart.GetNodeApproaching();
            var passedNode = normalizedStart.GetNodeJustPassed();
            var distanceToApproaching = normalizedStart.GetDistanceToNextNode();
            if (approachingNode == null)
            {
                return false;
            }
            if (!_provider.TryGetNode(approachingNode.NodeId, out approachingNode))
            {
                return false;
            }

            if (passedNode != null)
            {
                if (!_provider.TryGetNode(passedNode.NodeId, out passedNode))
                {
                    return false;
                }
            }
            else if (distanceToApproaching != 0)
            {
                return false;
            }

            // JP: 開始セグメントだけで到達できる場合は「未到達経路なし」。
            // EN: If the distance is reachable within the start segment, there is no unreached route.
            if (distance <= distanceToApproaching)
            {
                return false;
            }

            var remainingDistance = distance - distanceToApproaching;
            var forwardPath = new List<int> { approachingNode.NodeId };
            var pathStateGuard = new HashSet<(int nodeId, int remain)> { (approachingNode.NodeId, remainingDistance) };
            EnumerateForward(approachingNode.NodeId, remainingDistance);
            return resultRoutes.Count > 0;

            #region Internal

            void EnumerateForward(int currentNodeId, int remain)
            {
                if (isRouteLimitReached)
                {
                    return;
                }
                if (remain <= 0)
                {
                    return;
                }
                if (currentNodeId < 0 || currentNodeId >= _provider.ConnectNodes.Count)
                {
                    return;
                }

                var outgoing = BuildSortedOutgoingEdges(currentNodeId);
                var hasForwardCandidate = false;
                for (var i = 0; i < outgoing.Count; i++)
                {
                    var edge = outgoing[i];
                    if (edge.targetId < 0 || edge.distance < 0 || edge.targetId >= _provider.ConnectNodes.Count)
                    {
                        continue;
                    }
                    if (!_provider.TryGetNode(edge.targetId, out _))
                    {
                        continue;
                    }

                    // JP: この分岐で距離到達できるなら未到達候補にはしない。
                    // EN: This branch can reach the target distance, so it is not an unreached candidate.
                    if (remain <= edge.distance)
                    {
                        hasForwardCandidate = true;
                        continue;
                    }

                    var nextRemain = remain - edge.distance;
                    var nextState = (edge.targetId, nextRemain);
                    if (pathStateGuard.Contains(nextState))
                    {
                        continue;
                    }

                    hasForwardCandidate = true;
                    forwardPath.Add(edge.targetId);
                    pathStateGuard.Add(nextState);
                    EnumerateForward(edge.targetId, nextRemain);
                    pathStateGuard.Remove(nextState);
                    forwardPath.RemoveAt(forwardPath.Count - 1);
                    if (isRouteLimitReached)
                    {
                        return;
                    }
                }

                // JP: 残距離があるのに進めない分岐だけを未到達経路として採用する。
                // EN: Register only branches that still have remaining distance but cannot advance.
                if (!hasForwardCandidate)
                {
                    var reachedDistance = distance - remain;
                    if (reachedDistance < 0)
                    {
                        return;
                    }
                    if (TryCreateUnreachedRoute(forwardPath, currentNodeId, reachedDistance, out var route))
                    {
                        resultRoutes.Add(new UnreachedRoute(route, reachedDistance));
                        if (resultRoutes.Count >= DefaultMaxRouteCount)
                        {
                            isRouteLimitReached = true;
                        }
                    }
                }
            }

            bool TryCreateUnreachedRoute(
                IReadOnlyList<int> pathFromApproaching,
                int terminalApproachingNodeId,
                int routeLength,
                out RailPosition route)
            {
                route = null;
                if (pathFromApproaching == null || pathFromApproaching.Count <= 0 || terminalApproachingNodeId < 0 || routeLength < 0)
                {
                    return false;
                }

                var nodes = new List<IRailNode>();
                if (!_provider.TryGetNode(terminalApproachingNodeId, out var terminalNode))
                {
                    return false;
                }
                nodes.Add(terminalNode);

                var pathIndexStart = pathFromApproaching.Count - 1;
                if (pathFromApproaching[pathFromApproaching.Count - 1] == terminalApproachingNodeId)
                {
                    pathIndexStart = pathFromApproaching.Count - 2;
                }

                for (var i = pathIndexStart; i >= 0; i--)
                {
                    if (!_provider.TryGetNode(pathFromApproaching[i], out var node))
                    {
                        return false;
                    }
                    nodes.Add(node);
                }

                if (passedNode != null)
                {
                    nodes.Add(passedNode);
                }
                if (nodes.Count <= 0)
                {
                    return false;
                }

                route = new RailPosition(nodes, routeLength, 0);
                return true;
            }

            List<(int targetId, int distance)> BuildSortedOutgoingEdges(int nodeId)
            {
                var edges = _provider.ConnectNodes[nodeId];
                var sorted = new List<(int targetId, int distance)>(edges.Count);
                for (var i = 0; i < edges.Count; i++)
                {
                    sorted.Add(edges[i]);
                }
                sorted.Sort((left, right) =>
                    left.targetId != right.targetId
                        ? left.targetId.CompareTo(right.targetId)
                        : left.distance.CompareTo(right.distance));
                return sorted;
            }

            #endregion
        }
    }
}
