using System.Collections.Generic;

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
            // 入力を検証し中心セグメントを確認する
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

            // 前後距離を分割する
            // Split total length into front/back distances
            var frontLength = (totalLength + 1) / 2;
            var backLength = totalLength / 2;
            var distanceFromCenterToBehind = segmentDistance - centerOffsetToAhead;

            // 前方をトレースして先頭位置を決定する
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
                    // 分岐は決定的に1本を選択する
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

            // 後方をトレースして列車終端を確保する
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
                    // 分岐は決定的に1本を選択する
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

            // 先頭から末尾へノード列を構築する
            // Build node list from head to tail
            forwardPath.Reverse();
            var combinedNodeIds = new List<int>(forwardPath.Count + backwardPath.Count);
            combinedNodeIds.AddRange(forwardPath);
            combinedNodeIds.AddRange(backwardPath);

            // ConnectionDestination列に変換する
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
                // 前方候補を昇順で評価する
                // Evaluate forward candidates in ascending order
                var candidates = BuildSortedOutgoingEdges(nodeId);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var edge = candidates[i];
                    if (edge.distance <= 0 || visitedPath.Contains(edge.targetId))
                    {
                        continue;
                    }
                    // 1エッジで満たせる候補を確定する
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
                // 後方候補を昇順で評価する
                // Evaluate backward candidates in ascending order
                var candidates = BuildSortedIncomingEdges(nodeId);
                for (var i = 0; i < candidates.Count; i++)
                {
                    var edge = candidates[i];
                    if (edge.distance <= 0 || visitedPath.Contains(edge.sourceId))
                    {
                        continue;
                    }
                    // 1エッジで満たせる候補を確定する
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
                // 入力を検証しガードを更新する
                // Validate inputs and advance guard
                if (remainingDistance <= 0)
                {
                    return true;
                }
                if (guard > _provider.ConnectNodes.Count + 1)
                {
                    return false;
                }

                // 前方候補を昇順で探索する
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
                // 入力を検証しガードを更新する
                // Validate inputs and advance guard
                if (remainingDistance <= 0)
                {
                    return true;
                }
                if (guard > _provider.ConnectNodes.Count + 1)
                {
                    return false;
                }

                // 後方候補を昇順で探索する
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
                // 前方候補をソートして返す
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
                // 後方候補をソートして返す
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
    }
}
