using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal sealed class TrainCarPlacementSnapPointFinder
    {
        // 日本語: 要件2。中心点から最も近い駅ノードをスナップ開始点として求める。
        // English: Requirement-2 finder that resolves the nearest station node as snap start point.
        internal bool TryFindRequirement2NearestStationSnapPoint(
            RailPosition centerRailPosition,
            IReadOnlyList<RailPosition> frontRoutes,
            IReadOnlyList<RailPosition> rearRoutes,
            int frontMaxDistance,
            int rearMaxDistance,
            out RailPosition snapStartPoint)
        {
            snapStartPoint = null;
            if (centerRailPosition == null || frontRoutes == null || rearRoutes == null || frontMaxDistance < 0 || rearMaxDistance < 0)
            {
                return false;
            }

            var centerForwardPoint = centerRailPosition.GetHeadRailPosition();
            var centerBackwardPoint = centerForwardPoint.DeepCopy();
            centerBackwardPoint.Reverse();

            var nearestDistance = int.MaxValue;
            var nearestPoint = default(RailPosition);
            EvaluateRouteList(frontRoutes, frontMaxDistance);
            EvaluateRouteList(rearRoutes, rearMaxDistance);
            if (nearestPoint == null)
            {
                return false;
            }

            snapStartPoint = nearestPoint;
            return true;

            #region Internal

            void EvaluateRouteList(IReadOnlyList<RailPosition> routes, int maxDistance)
            {
                for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
                {
                    var route = routes[routeIndex];
                    if (route == null)
                    {
                        continue;
                    }
                    var nodes = route.GetRailNodes();
                    if (nodes == null || nodes.Count <= 0)
                    {
                        continue;
                    }

                    for (var nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                    {
                        var node = nodes[nodeIndex];
                        if (node == null || node.StationRef == null || !node.StationRef.HasStation)
                        {
                            continue;
                        }
                        if (!TryCreateRequirement2PointAtRouteNode(route, nodeIndex, out var stationPoint))
                        {
                            continue;
                        }
                        if (!TryOrientPointTowardCenter(stationPoint, centerForwardPoint, centerBackwardPoint, out var orientedPoint, out var distanceToCenter))
                        {
                            continue;
                        }
                        // 中心点からの距離が探索半径外なら要件2候補にしない
                        // Exclude nodes outside the requirement-2 search radius from center
                        if (distanceToCenter > maxDistance)
                        {
                            continue;
                        }
                        if (distanceToCenter >= nearestDistance)
                        {
                            continue;
                        }
                        nearestDistance = distanceToCenter;
                        nearestPoint = orientedPoint;
                    }
                }
            }

            #endregion
        }

        // 日本語: 要件3。未到達経路のうち中心点に最も近いレール端をスナップ開始点として求める。
        // English: Requirement-3 finder that resolves the nearest unreached rail-end as snap start point.
        internal bool TryFindRequirement3NearestRailEndSnapPoint(
            RailPosition centerRailPosition,
            IReadOnlyList<RailPathTracer.UnreachedRoute> frontUnreachedRoutes,
            IReadOnlyList<RailPathTracer.UnreachedRoute> rearUnreachedRoutes,
            out RailPosition snapStartPoint)
        {
            snapStartPoint = null;
            if (centerRailPosition == null)
            {
                return false;
            }

            var centerForwardPoint = centerRailPosition.GetHeadRailPosition();
            var centerBackwardPoint = centerForwardPoint.DeepCopy();
            centerBackwardPoint.Reverse();

            var nearestReachedDistance = int.MaxValue;
            var nearestPoint = default(RailPosition);
            EvaluateUnreachedRoutes(frontUnreachedRoutes);
            EvaluateUnreachedRoutes(rearUnreachedRoutes);
            if (nearestPoint == null)
            {
                return false;
            }

            snapStartPoint = nearestPoint;
            return true;

            #region Internal

            void EvaluateUnreachedRoutes(IReadOnlyList<RailPathTracer.UnreachedRoute> routes)
            {
                if (routes == null)
                {
                    return;
                }

                for (var i = 0; i < routes.Count; i++)
                {
                    var candidate = routes[i];
                    if (candidate.Route == null || candidate.ReachedDistance < 0)
                    {
                        continue;
                    }
                    var edgePoint = candidate.Route.GetHeadRailPosition();
                    if (!TryOrientPointTowardCenter(edgePoint, centerForwardPoint, centerBackwardPoint, out var orientedPoint, out _))
                    {
                        continue;
                    }
                    if (candidate.ReachedDistance >= nearestReachedDistance)
                    {
                        continue;
                    }
                    nearestReachedDistance = candidate.ReachedDistance;
                    nearestPoint = orientedPoint;
                }
            }

            #endregion
        }

        #region Internal

        private static bool TryCreateRequirement2PointAtRouteNode(RailPosition route, int nodeIndex, out RailPosition point)
        {
            point = null;
            if (route == null)
            {
                return false;
            }

            var nodes = route.GetRailNodes();
            if (nodes == null || nodeIndex < 0 || nodeIndex >= nodes.Count)
            {
                return false;
            }
            if (nodes[nodeIndex] == null)
            {
                return false;
            }
            if (nodes.Count == 1)
            {
                point = new RailPosition(new List<IRailNode> { nodes[0] }, 0, 0);
                return true;
            }

            if (nodeIndex < nodes.Count - 1)
            {
                if (nodes[nodeIndex + 1] == null)
                {
                    return false;
                }
                var nextToCurrentDistance = nodes[nodeIndex + 1].GetDistanceToNode(nodes[nodeIndex]);
                if (nextToCurrentDistance < 0)
                {
                    return false;
                }
                point = new RailPosition(new List<IRailNode> { nodes[nodeIndex], nodes[nodeIndex + 1] }, 0, 0);
                return true;
            }

            if (nodes[nodeIndex - 1] == null)
            {
                return false;
            }
            var currentToPrevDistance = nodes[nodeIndex].GetDistanceToNode(nodes[nodeIndex - 1]);
            if (currentToPrevDistance < 0)
            {
                return false;
            }

            // 最後尾ノードを点として表すには、ひとつ前の辺長をdistanceToNextへ入れて正規化させる
            // To represent the tail node as a point, use previous edge length as distanceToNext then normalize
            point = new RailPosition(new List<IRailNode> { nodes[nodeIndex - 1], nodes[nodeIndex] }, 0, currentToPrevDistance);
            return true;
        }

        private static bool TryOrientPointTowardCenter(
            RailPosition point,
            RailPosition centerForwardPoint,
            RailPosition centerBackwardPoint,
            out RailPosition orientedPoint,
            out int distanceToCenter)
        {
            if (point == null || centerForwardPoint == null || centerBackwardPoint == null)
            {
                orientedPoint = null;
                distanceToCenter = int.MaxValue;
                return false;
            }

            var bestPoint = default(RailPosition);
            var bestDistance = int.MaxValue;
            Update(point);

            var reversed = point.DeepCopy();
            reversed.Reverse();
            Update(reversed);
            orientedPoint = bestPoint;
            distanceToCenter = bestDistance;
            return bestPoint != null;

            #region Internal

            void Update(RailPosition candidate)
            {
                if (candidate == null)
                {
                    return;
                }
                var candidateDistance = FindDistanceToCenter(candidate, centerForwardPoint, centerBackwardPoint);
                if (candidateDistance < 0 || candidateDistance >= bestDistance)
                {
                    return;
                }
                bestPoint = candidate.DeepCopy();
                bestDistance = candidateDistance;
            }

            #endregion
        }

        private static int FindDistanceToCenter(RailPosition fromPoint, RailPosition centerForwardPoint, RailPosition centerBackwardPoint)
        {
            var forwardDistance = RailPositionRouteDistanceFinder.FindShortestDistance(fromPoint, centerForwardPoint);
            var backwardDistance = RailPositionRouteDistanceFinder.FindShortestDistance(fromPoint, centerBackwardPoint);
            if (forwardDistance < 0)
            {
                return backwardDistance;
            }
            if (backwardDistance < 0)
            {
                return forwardDistance;
            }
            return forwardDistance < backwardDistance ? forwardDistance : backwardDistance;
        }

        #endregion
    }
}
