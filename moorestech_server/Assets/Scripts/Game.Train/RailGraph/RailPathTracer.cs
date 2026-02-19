using System.Collections.Generic;
using Game.Train.RailPositions;
using Game.Train.SaveLoad;

namespace Game.Train.RailGraph
{
    public readonly struct RailPathTraceResult
    {
        public RailPathTraceResult(List<int> nodeIds, List<ConnectionDestination> railSnapshot, int distanceToNextNode, int consumedForwardDistance, int consumedBackwardDistance)
        {
            NodeIds = nodeIds;
            RailSnapshot = railSnapshot;
            DistanceToNextNode = distanceToNextNode;
            ConsumedForwardDistance = consumedForwardDistance;
            ConsumedBackwardDistance = consumedBackwardDistance;
        }

        public List<int> NodeIds { get; }
        public List<ConnectionDestination> RailSnapshot { get; }
        public int DistanceToNextNode { get; }
        public int ConsumedForwardDistance { get; }
        public int ConsumedBackwardDistance { get; }
    }

    public sealed class RailPathTracer
    {
        private readonly IRailGraphTraversalProvider _provider;

        public RailPathTracer(IRailGraphTraversalProvider provider)
        {
            _provider = provider;
        }

        public bool TryTraceCentered(int behindNodeId, int aheadNodeId, int centerOffsetToAhead, int totalLength, out RailPathTraceResult result)
        {
            result = default;
            // 入力を検証し、中心セグメントを確認する。
            // Validate inputs and confirm the center segment
            if (!_provider.TryGetNode(behindNodeId, out var behindNode) || !_provider.TryGetNode(aheadNodeId, out var aheadNode))
            {
                return false;
            }
            if (totalLength <= 0)
            {
                return false;
            }

            var segmentDistance = behindNode.GetDistanceToNode(aheadNode);
            if (segmentDistance <= 0 || centerOffsetToAhead < 0 || centerOffsetToAhead > segmentDistance)
            {
                return false;
            }

            // 総距離を前方・後方距離へ分割する。
            // Split total length into front/back distances
            var frontLength = (totalLength + 1) / 2;
            var backLength = totalLength / 2;
            var distanceFromCenterToBehind = segmentDistance - centerOffsetToAhead;

            // 前方を探索して先頭位置を決定する。
            // Trace forward to determine the head position
            var forwardPath = new List<int> { aheadNodeId };
            var forwardConsumed = 0;
            var distanceToNextNode = 0;
            if (frontLength <= centerOffsetToAhead)
            {
                distanceToNextNode = centerOffsetToAhead - frontLength;
                forwardConsumed = frontLength;
            }
            else
            {
                var remainingForward = frontLength - centerOffsetToAhead;
                forwardConsumed = centerOffsetToAhead;
                var currentNodeId = aheadNodeId;
                var visited = new HashSet<int> { aheadNodeId };
                var guard = 0;
                while (true)
                {
                    // 分岐では決定的な出辺を1本選択する。
                    // Pick a deterministic outgoing edge at branches
                    if (!TrySelectForwardEdge(currentNodeId, remainingForward, visited, out var nextNodeId, out var edgeDistance))
                    {
                        return false;
                    }
                    if (!visited.Add(nextNodeId) || edgeDistance <= 0)
                    {
                        return false;
                    }
                    forwardPath.Add(nextNodeId);
                    if (remainingForward <= edgeDistance)
                    {
                        distanceToNextNode = edgeDistance - remainingForward;
                        forwardConsumed += remainingForward;
                        break;
                    }
                    remainingForward -= edgeDistance;
                    forwardConsumed += edgeDistance;
                    currentNodeId = nextNodeId;
                    guard++;
                    if (guard > _provider.ConnectNodes.Count + 1)
                    {
                        return false;
                    }
                }
            }

            // 後方を探索して列車後端範囲を確保する。
            // Trace backward to cover the train tail
            var backwardPath = new List<int> { behindNodeId };
            var backwardConsumed = 0;
            if (backLength <= distanceFromCenterToBehind)
            {
                backwardConsumed = backLength;
            }
            else
            {
                var remainingBack = backLength - distanceFromCenterToBehind;
                backwardConsumed = distanceFromCenterToBehind;
                var currentNodeId = behindNodeId;
                var visited = new HashSet<int> { behindNodeId };
                var guard = 0;
                while (true)
                {
                    // 分岐では決定的な入辺を1本選択する。
                    // Pick a deterministic incoming edge at branches
                    if (!TrySelectIncomingEdge(currentNodeId, remainingBack, visited, out var previousNodeId, out var edgeDistance))
                    {
                        return false;
                    }
                    if (!visited.Add(previousNodeId) || edgeDistance <= 0)
                    {
                        return false;
                    }
                    backwardPath.Add(previousNodeId);
                    if (remainingBack <= edgeDistance)
                    {
                        backwardConsumed += remainingBack;
                        break;
                    }
                    remainingBack -= edgeDistance;
                    backwardConsumed += edgeDistance;
                    currentNodeId = previousNodeId;
                    guard++;
                    if (guard > _provider.ConnectNodes.Count + 1)
                    {
                        return false;
                    }
                }
            }

            // 先頭から末尾までのノード列を構築する。
            // Build node list from head to tail
            forwardPath.Reverse();
            var combinedNodeIds = new List<int>(forwardPath.Count + backwardPath.Count);
            combinedNodeIds.AddRange(forwardPath);
            combinedNodeIds.AddRange(backwardPath);

            // ノード列をConnectionDestination列へ変換する。
            // Convert node list into ConnectionDestination list
            var railSnapshot = new List<ConnectionDestination>(combinedNodeIds.Count);
            for (var i = 0; i < combinedNodeIds.Count; i++)
            {
                if (!_provider.TryGetNode(combinedNodeIds[i], out var node))
                {
                    return false;
                }
                railSnapshot.Add(node.ConnectionDestination);
            }

            result = new RailPathTraceResult(combinedNodeIds, railSnapshot, distanceToNextNode, forwardConsumed, backwardConsumed);
            return true;

            #region Internal

            bool TrySelectForwardEdge(int nodeId, int remainingDistance, HashSet<int> visitedPath, out int nextNodeId, out int distance)
            {
                nextNodeId = -1;
                distance = 0;
                // 前方候補を昇順で評価する。
                // Evaluate forward candidates in ascending order
                var candidates = BuildSortedOutgoingEdges(nodeId);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var edge = candidates[i];
                    if (edge.distance <= 0 || visitedPath.Contains(edge.targetId))
                    {
                        continue;
                    }
                    // 1辺で成立する候補を確定する。
                    // Lock in a candidate that fits within one edge
                    if (remainingDistance <= edge.distance)
                    {
                        nextNodeId = edge.targetId;
                        distance = edge.distance;
                        return true;
                    }
                    var nextVisited = new HashSet<int>(visitedPath) { edge.targetId };
                    if (CanConsumeForwardDistance(edge.targetId, remainingDistance - edge.distance, nextVisited, 0))
                    {
                        nextNodeId = edge.targetId;
                        distance = edge.distance;
                        return true;
                    }
                }
                return false;
            }

            bool TrySelectIncomingEdge(int nodeId, int remainingDistance, HashSet<int> visitedPath, out int previousNodeId, out int distance)
            {
                previousNodeId = -1;
                distance = 0;
                // 後方候補を昇順で評価する。
                // Evaluate backward candidates in ascending order
                var candidates = BuildSortedIncomingEdges(nodeId);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var edge = candidates[i];
                    if (edge.distance <= 0 || visitedPath.Contains(edge.sourceId))
                    {
                        continue;
                    }
                    // 1辺で成立する候補を確定する。
                    // Lock in a candidate that fits within one edge
                    if (remainingDistance <= edge.distance)
                    {
                        previousNodeId = edge.sourceId;
                        distance = edge.distance;
                        return true;
                    }
                    var nextVisited = new HashSet<int>(visitedPath) { edge.sourceId };
                    if (CanConsumeBackwardDistance(edge.sourceId, remainingDistance - edge.distance, nextVisited, 0))
                    {
                        previousNodeId = edge.sourceId;
                        distance = edge.distance;
                        return true;
                    }
                }
                return false;
            }

            bool CanConsumeForwardDistance(int startNodeId, int remainingDistance, HashSet<int> visitedPath, int guard)
            {
                // 入力を検証し、ガードを進める。
                // Validate inputs and advance guard
                if (remainingDistance <= 0)
                {
                    return true;
                }
                if (guard > _provider.ConnectNodes.Count + 1)
                {
                    return false;
                }

                // 前方候補を昇順で探索する。
                // Explore forward candidates in ascending order
                var candidates = BuildSortedOutgoingEdges(startNodeId);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var edge = candidates[i];
                    if (edge.distance <= 0 || visitedPath.Contains(edge.targetId))
                    {
                        continue;
                    }
                    if (remainingDistance <= edge.distance)
                    {
                        return true;
                    }
                    visitedPath.Add(edge.targetId);
                    if (CanConsumeForwardDistance(edge.targetId, remainingDistance - edge.distance, visitedPath, guard + 1))
                    {
                        return true;
                    }
                    visitedPath.Remove(edge.targetId);
                }
                return false;
            }

            bool CanConsumeBackwardDistance(int startNodeId, int remainingDistance, HashSet<int> visitedPath, int guard)
            {
                // 入力を検証し、ガードを進める。
                // Validate inputs and advance guard
                if (remainingDistance <= 0)
                {
                    return true;
                }
                if (guard > _provider.ConnectNodes.Count + 1)
                {
                    return false;
                }

                // 後方候補を昇順で探索する。
                // Explore backward candidates in ascending order
                var candidates = BuildSortedIncomingEdges(startNodeId);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var edge = candidates[i];
                    if (edge.distance <= 0 || visitedPath.Contains(edge.sourceId))
                    {
                        continue;
                    }
                    if (remainingDistance <= edge.distance)
                    {
                        return true;
                    }
                    visitedPath.Add(edge.sourceId);
                    if (CanConsumeBackwardDistance(edge.sourceId, remainingDistance - edge.distance, visitedPath, guard + 1))
                    {
                        return true;
                    }
                    visitedPath.Remove(edge.sourceId);
                }
                return false;
            }

            List<(int targetId, int distance)> BuildSortedOutgoingEdges(int nodeId)
            {
                // 出辺候補をソートして返す。
                // Sort outgoing candidates and return them
                var edges = _provider.ConnectNodes[nodeId];
                var result = new List<(int targetId, int distance)>(edges.Count);
                for (var i = 0; i < edges.Count; i++)
                {
                    result.Add(edges[i]);
                }
                result.Sort((left, right) => left.targetId != right.targetId ? left.targetId.CompareTo(right.targetId) : left.distance.CompareTo(right.distance));
                return result;
            }

            List<(int sourceId, int distance)> BuildSortedIncomingEdges(int nodeId)
            {
                // 入辺候補をソートして返す。
                // Sort incoming candidates and return them
                var result = new List<(int sourceId, int distance)>();
                for (var i = 0; i < _provider.ConnectNodes.Count; i++)
                {
                    var edges = _provider.ConnectNodes[i];
                    for (var j = 0; j < edges.Count; j++)
                    {
                        var edge = edges[j];
                        if (edge.targetId != nodeId)
                        {
                            continue;
                        }
                        result.Add((i, edge.distance));
                    }
                }
                result.Sort((left, right) => left.sourceId != right.sourceId ? left.sourceId.CompareTo(right.sourceId) : left.distance.CompareTo(right.distance));
                return result;
            }

            #endregion
        }

        public bool TryTraceForwardRoutesByDfs(RailPosition startPoint, int distance, out List<RailPosition> routes)
        {
            routes = new List<RailPosition>();
            var resultRoutes = routes;

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
                // JP: 残距離0は現ノード終端とし、0距離先ノードには進まない。
                // EN: Remaining zero terminates at current node and does not step into zero-length successors.
                if (remain == 0)
                {
                    if (TryCreateRouteTerminated(forwardPath, currentNodeId, 0, out var routeAtNode))
                    {
                        resultRoutes.Add(routeAtNode);
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
    }
}

