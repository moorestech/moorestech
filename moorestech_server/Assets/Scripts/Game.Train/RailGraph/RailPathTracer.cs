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
                    if (!TrySelectForwardEdge(currentNodeId, out var nextNodeId, out var edgeDistance))
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
                    if (!TrySelectIncomingEdge(currentNodeId, out var previousNodeId, out var edgeDistance))
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

            bool TrySelectForwardEdge(int nodeId, out int nextNodeId, out int distance)
            {
                nextNodeId = -1;
                distance = 0;
                var edges = _provider.ConnectNodes[nodeId];
                var found = false;
                for (var i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (!found || edge.targetId < nextNodeId || (edge.targetId == nextNodeId && edge.distance < distance))
                    {
                        nextNodeId = edge.targetId;
                        distance = edge.distance;
                        found = true;
                    }
                }
                return found;
            }

            bool TrySelectIncomingEdge(int nodeId, out int previousNodeId, out int distance)
            {
                previousNodeId = -1;
                distance = 0;
                var found = false;
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
                        if (!found || i < previousNodeId || (i == previousNodeId && edge.distance < distance))
                        {
                            previousNodeId = i;
                            distance = edge.distance;
                            found = true;
                        }
                    }
                }
                return found;
            }

            #endregion
        }
    }
}
