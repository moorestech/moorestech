using System;
using Game.Train.RailCalc;
using Game.Train.RailGraph;

namespace Game.Train.RailPositions
{
    public static class RailPositionRouteDistanceFinder
    {
        public static int FindShortestDistance(RailPosition start, RailPosition end)
        {
            if (start == null || end == null) return -1;

            // 日本語: 列車長の影響を除外し、先頭点同士の距離計算に正規化する。
            // English: Normalize both inputs to head points to ignore train length effects.
            var startHead = start.GetHeadRailPosition();
            var endHead = end.GetHeadRailPosition();
            if (startHead.IsSamePositionAllowNodeOverlap(endHead))
            {
                return 0;
            }

            if (!TryCreatePoint(startHead, out var startPoint)) return -1;
            if (!TryCreatePoint(endHead, out var endPoint)) return -1;

            var minDistance = int.MaxValue;

            // 日本語: 始点はapproaching側へ前進し、終点はjustPassed側から進入して到達する経路を評価する。
            // English: Evaluate routed movement from start approaching side to end entry side.
            var endEntryNode = endPoint.HasSegment ? endPoint.JustPassedNode : endPoint.ApproachingNode;
            var endTailDistance = endPoint.HasSegment ? endPoint.DistanceFromJustPassed : 0;
            if (TryCalculateNodePathDistance(startPoint.ApproachingNode, endEntryNode, out var nodePathDistance))
            {
                var routedDistanceLong = (long)startPoint.DistanceToApproaching + nodePathDistance + endTailDistance;
                if (routedDistanceLong <= int.MaxValue)
                {
                    minDistance = (int)Math.Min(minDistance, routedDistanceLong);
                }
            }

            // 日本語: 同一セグメント上で前方向に終点へ到達できる場合は、ノードを経由しない直進距離を候補に含める。
            // English: Add direct same-segment forward distance when reachable without touching nodes.
            if (TryCalculateDirectSameSegmentDistance(startPoint, endPoint, out var directDistance))
            {
                minDistance = Math.Min(minDistance, directDistance);
            }

            return minDistance == int.MaxValue ? -1 : minDistance;
        }

        private static bool TryCreatePoint(RailPosition position, out RailPoint point)
        {
            point = default;
            if (position == null) return false;

            var approachingNode = position.GetNodeApproaching();
            if (approachingNode == null) return false;

            var distanceToApproaching = position.GetDistanceToNextNode();
            if (distanceToApproaching < 0) return false;

            var justPassedNode = position.GetNodeJustPassed();
            if (justPassedNode == null)
            {
                if (distanceToApproaching != 0) return false;
                point = new RailPoint(approachingNode, null, 0, 0, false);
                return true;
            }

            var segmentLength = justPassedNode.GetDistanceToNode(approachingNode);
            if (segmentLength < 0) return false;
            if (distanceToApproaching > segmentLength) return false;

            var distanceFromJustPassed = segmentLength - distanceToApproaching;
            point = new RailPoint(approachingNode, justPassedNode, distanceToApproaching, distanceFromJustPassed, true);
            return true;
        }

        private static bool TryCalculateNodePathDistance(IRailNode startNode, IRailNode endNode, out int distance)
        {
            distance = -1;
            if (startNode == null || endNode == null) return false;

            var path = startNode.GraphProvider.FindShortestPath(startNode, endNode);
            if (path == null || path.Count == 0) return false;
            if (path[0] != startNode) return false;
            if (path[path.Count - 1] != endNode) return false;

            for (var i = 0; i < path.Count; i++)
            {
                if (path[i] == null) return false;
            }

            distance = RailNodeCalculate.CalculateTotalDistanceF(path);
            return true;
        }

        private static bool TryCalculateDirectSameSegmentDistance(RailPoint startPoint, RailPoint endPoint, out int distance)
        {
            distance = -1;
            if (!startPoint.HasSegment || !endPoint.HasSegment) return false;
            if (startPoint.ApproachingNode != endPoint.ApproachingNode) return false;
            if (startPoint.JustPassedNode != endPoint.JustPassedNode) return false;
            if (startPoint.DistanceToApproaching < endPoint.DistanceToApproaching) return false;

            distance = startPoint.DistanceToApproaching - endPoint.DistanceToApproaching;
            return true;
        }

        private readonly struct RailPoint
        {
            public readonly IRailNode ApproachingNode;
            public readonly IRailNode JustPassedNode;
            public readonly int DistanceToApproaching;
            public readonly int DistanceFromJustPassed;
            public readonly bool HasSegment;

            public RailPoint(
                IRailNode approachingNode,
                IRailNode justPassedNode,
                int distanceToApproaching,
                int distanceFromJustPassed,
                bool hasSegment)
            {
                ApproachingNode = approachingNode;
                JustPassedNode = justPassedNode;
                DistanceToApproaching = distanceToApproaching;
                DistanceFromJustPassed = distanceFromJustPassed;
                HasSegment = hasSegment;
            }
        }
    }
}
