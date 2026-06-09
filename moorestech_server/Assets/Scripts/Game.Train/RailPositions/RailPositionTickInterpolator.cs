using System;
using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.RailPositions
{
    public static class RailPositionTickInterpolator
    {
        private const int MoveLoopGuardThreshold = 1000000;

        public static bool TryInterpolateByTick(
            RailPosition previous,
            RailPosition current,
            IRailGraphProvider railGraphProvider,
            IRailGraphTraversalProvider traversalProvider,
            double previousTick,
            double currentTick,
            double renderTick,
            out RailPosition interpolated)
        {
            interpolated = null;
            if (currentTick <= previousTick)
            {
                return false;
            }

            // 前後 tick から距離比率を作る。
            // Build distance weights from the surrounding ticks.
            var previousWeight = currentTick - renderTick;
            var currentWeight = renderTick - previousTick;
            return TryInterpolateByWeight(
                previous,
                current,
                railGraphProvider,
                traversalProvider,
                previousWeight,
                currentWeight,
                out interpolated);
        }

        public static bool TryInterpolateByWeight(
            RailPosition previous,
            RailPosition current,
            IRailGraphProvider railGraphProvider,
            IRailGraphTraversalProvider traversalProvider,
            double previousWeight,
            double currentWeight,
            out RailPosition interpolated)
        {
            interpolated = null;
            if (previous == null || current == null || railGraphProvider == null || traversalProvider == null)
            {
                return false;
            }

            // 重みは前後距離の a:b として扱う。
            // Treat weights as the previous/current distance ratio.
            if (previousWeight < 0 || currentWeight < 0)
            {
                return false;
            }
            var totalWeight = previousWeight + currentWeight;
            if (totalWeight <= 0)
            {
                return false;
            }

            // 両端 RailPosition を現在 graph 上に投影する。
            // Reproject both endpoint RailPositions onto the current graph.
            if (!TryReprojectToCurrentGraph(previous, railGraphProvider, out var projectedPrevious))
            {
                return false;
            }
            if (!TryReprojectToCurrentGraph(current, railGraphProvider, out var projectedCurrent))
            {
                return false;
            }
            if (projectedPrevious.TrainLength != projectedCurrent.TrainLength)
            {
                return false;
            }

            // 比率端では余計な経路探索をせず端点を返す。
            // Return endpoint copies directly at ratio boundaries.
            var currentRatio = currentWeight / totalWeight;
            if (currentRatio <= 0)
            {
                interpolated = projectedPrevious.DeepCopy();
                return true;
            }
            if (currentRatio >= 1)
            {
                interpolated = projectedCurrent.DeepCopy();
                return true;
            }

            // 旧 head から新 head への最短距離を現在 graph で測る。
            // Measure the shortest current-graph distance between the old and new heads.
            var previousHead = projectedPrevious.GetHeadRailPosition();
            var currentHead = projectedCurrent.GetHeadRailPosition();
            var totalDistance = RailPositionRouteDistanceFinder.FindShortestDistance(previousHead, currentHead);
            if (totalDistance < 0)
            {
                return false;
            }
            if (totalDistance == 0)
            {
                interpolated = projectedCurrent.DeepCopy();
                return true;
            }

            // 距離比率を整数 rail unit に丸める。
            // Convert the ratio into integer rail units.
            var moveDistance = (int)Math.Round(totalDistance * currentRatio, MidpointRounding.AwayFromZero);
            moveDistance = Clamp(moveDistance, 0, totalDistance);
            if (moveDistance <= 0)
            {
                interpolated = projectedPrevious.DeepCopy();
                return true;
            }
            if (moveDistance >= totalDistance)
            {
                interpolated = projectedCurrent.DeepCopy();
                return true;
            }

            // full RailPosition を最短経路上で進め、車両長を保った描画用位置を作る。
            // Move the full RailPosition on the shortest route while preserving train length.
            return TryMoveAlongShortestPath(
                projectedPrevious,
                currentHead,
                railGraphProvider,
                traversalProvider,
                moveDistance,
                out interpolated);
        }

        private static bool TryReprojectToCurrentGraph(RailPosition source, IRailGraphProvider railGraphProvider, out RailPosition reprojected)
        {
            reprojected = null;
            var snapshot = source.CreateSaveSnapshot();
            if (snapshot.RailSnapshot == null || snapshot.RailSnapshot.Count == 0)
            {
                return false;
            }

            // snapshot node は1つでも解決できなければ補間不能にする。
            // Fail interpolation when even one snapshot node cannot be resolved.
            var anchors = new List<IRailNode>(snapshot.RailSnapshot.Count);
            for (var i = 0; i < snapshot.RailSnapshot.Count; i++)
            {
                var node = railGraphProvider.ResolveRailNode(snapshot.RailSnapshot[i]);
                if (node == null)
                {
                    return false;
                }
                anchors.Add(node);
            }

            // 元 route の node 間は現在 graph の最短経路でつなぎ直す。
            // Reconnect source-route anchors by current-graph shortest paths.
            if (!TryExpandShortestRoute(anchors, railGraphProvider, out var expandedNodes))
            {
                return false;
            }
            if (!CanCoverRailPosition(expandedNodes, snapshot.DistanceToNextNode, snapshot.TrainLength))
            {
                return false;
            }

            reprojected = new RailPosition(expandedNodes, snapshot.TrainLength, snapshot.DistanceToNextNode);
            return true;
        }

        private static bool TryExpandShortestRoute(IReadOnlyList<IRailNode> anchors, IRailGraphProvider railGraphProvider, out List<IRailNode> expandedNodes)
        {
            expandedNodes = new List<IRailNode>();
            if (anchors == null || anchors.Count == 0)
            {
                return false;
            }

            expandedNodes.Add(anchors[0]);
            for (var i = 0; i < anchors.Count - 1; i++)
            {
                var frontSideNode = anchors[i];
                var rearSideNode = anchors[i + 1];
                if (IsSameNode(frontSideNode, rearSideNode))
                {
                    continue;
                }

                // RailPosition の node 列は head 側から rear 側なので、探索結果を反転して追加する。
                // RailPosition nodes are ordered head-to-rear, so reverse the shortest path before appending.
                var path = railGraphProvider.FindShortestPath(rearSideNode, frontSideNode);
                if (path == null || path.Count == 0)
                {
                    return false;
                }
                for (var pathIndex = path.Count - 2; pathIndex >= 0; pathIndex--)
                {
                    var node = path[pathIndex];
                    if (node == null)
                    {
                        return false;
                    }
                    expandedNodes.Add(node);
                }
            }
            return expandedNodes.Count > 0;
        }

        private static bool TryMoveAlongShortestPath(
            RailPosition start,
            RailPosition targetHead,
            IRailGraphProvider railGraphProvider,
            IRailGraphTraversalProvider traversalProvider,
            int moveDistance,
            out RailPosition moved)
        {
            moved = start.DeepCopy();
            var pathTargetNode = ResolveDistanceTargetNode(targetHead);
            var finalApproachingNode = targetHead.GetNodeApproaching();
            if (pathTargetNode == null || finalApproachingNode == null)
            {
                moved = null;
                return false;
            }

            // 既存 RailPosition.MoveForward を使い、必要なときだけ最短経路の次 node を先頭へ積む。
            // Use existing RailPosition.MoveForward and add only the next shortest-path node when needed.
            var remainingDistance = moveDistance;
            var loopCount = 0;
            while (remainingDistance > 0)
            {
                var movedDistance = moved.MoveForward(remainingDistance);
                remainingDistance -= movedDistance;
                if (remainingDistance <= 0)
                {
                    return true;
                }

                var approaching = moved.GetNodeApproaching();
                if (approaching == null)
                {
                    moved = null;
                    return false;
                }

                // target が segment 途中なら、justPassed 到達後に final approaching へ切り替える。
                // For an in-segment target, switch to the final approaching node after reaching justPassed.
                if (IsSameNode(approaching, pathTargetNode) && !IsSameNode(pathTargetNode, finalApproachingNode))
                {
                    pathTargetNode = finalApproachingNode;
                    continue;
                }

                if (!TryFindNextNodeOnShortestPath(approaching, pathTargetNode, railGraphProvider, traversalProvider, out var nextNode))
                {
                    moved = null;
                    return false;
                }
                moved.AddNodeToHead(nextNode);

                loopCount++;
                if (loopCount > MoveLoopGuardThreshold)
                {
                    moved = null;
                    return false;
                }
            }

            return true;
        }

        private static bool TryFindNextNodeOnShortestPath(
            IRailNode approaching,
            IRailNode target,
            IRailGraphProvider railGraphProvider,
            IRailGraphTraversalProvider traversalProvider,
            out IRailNode nextNode)
        {
            nextNode = null;
            if (!traversalProvider.TryGetNode(approaching.NodeId, out var currentNode))
            {
                return false;
            }
            if (!traversalProvider.TryGetNode(target.NodeId, out var currentTarget))
            {
                return false;
            }

            // 現在 graph の shortest path から次の1 node だけを採用する。
            // Take only the next node from the current-graph shortest path.
            var path = railGraphProvider.FindShortestPath(currentNode, currentTarget);
            if (path == null || path.Count < 2)
            {
                return false;
            }
            nextNode = path[1];
            return nextNode != null;
        }

        private static IRailNode ResolveDistanceTargetNode(RailPosition targetHead)
        {
            if (targetHead.GetDistanceToNextNode() != 0)
            {
                return targetHead.GetNodeJustPassed();
            }
            return targetHead.GetNodeApproaching();
        }

        private static bool CanCoverRailPosition(IReadOnlyList<IRailNode> nodes, int distanceToNextNode, int trainLength)
        {
            if (nodes == null || nodes.Count == 0 || distanceToNextNode < 0 || trainLength < 0)
            {
                return false;
            }
            if (nodes.Count == 1)
            {
                return distanceToNextNode == 0 && trainLength == 0;
            }

            // RailPosition が要求する head/rear span を現在 graph の node 列が覆えるか確認する。
            // Verify that the current-graph node chain covers the span required by RailPosition.
            var totalDistance = 0;
            for (var i = 0; i < nodes.Count - 1; i++)
            {
                var distance = nodes[i + 1].GetDistanceToNode(nodes[i]);
                if (distance < 0)
                {
                    return false;
                }
                totalDistance += distance;
            }
            return totalDistance >= distanceToNextNode + trainLength;
        }

        private static bool IsSameNode(IRailNode left, IRailNode right)
        {
            return left != null && right != null && left.NodeId == right.NodeId;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            return value > max ? max : value;
        }
    }
}
