using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarPlacementSnapPointFinder
    {
        // 日本語: 中心点から最も近い駅ノードをスナップ開始点として求める。
        // English: Resolve the nearest station node from center as the snap start point.
        internal static bool TryFindNearestStationSnapPoint(
            RailPosition centerRailPosition,
            IReadOnlyList<RailPosition> frontRoutes,
            IReadOnlyList<RailPosition> rearRoutes,
            int frontMaxDistance,
            int rearMaxDistance,
            out RailPosition snapStartPoint,
            out bool snapFromCenterForward)
        {
            snapStartPoint = null;
            snapFromCenterForward = true;
            if (centerRailPosition == null || frontRoutes == null || rearRoutes == null || frontMaxDistance < 0 || rearMaxDistance < 0)
            {
                return false;
            }

            var centerForwardPoint = centerRailPosition.GetHeadRailPosition();
            var centerBackwardPoint = centerForwardPoint.DeepCopy();
            centerBackwardPoint.Reverse();
            
            RailPosition nearestPoint = null;
            int nearestDistance = frontMaxDistance;
            var hasFrontCandidateFront = EvaluateRouteList(
                frontRoutes,
                centerForwardPoint,
                ref nearestPoint,
                ref nearestDistance);
            if (!hasFrontCandidateFront)
            {
                nearestDistance = rearMaxDistance;
            }
            var hasFrontCandidateBack = EvaluateRouteList(
                rearRoutes,
                centerBackwardPoint,
                ref nearestPoint,
                ref nearestDistance);
            if (hasFrontCandidateBack)
            {
                snapFromCenterForward = false;
            }
            
            snapStartPoint = nearestPoint;
            return hasFrontCandidateFront || hasFrontCandidateBack;

            #region Internal

            bool EvaluateRouteList(
                IReadOnlyList<RailPosition> routes,
                RailPosition centerPoint,
                ref RailPosition nearestPoint,
                ref int nearestDistance)
            {
                if (routes == null || centerPoint == null)
                {
                    return false;
                }
                bool hasCandidate = false;

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
                        var stationPoint = new RailPosition(new List<IRailNode> { node }, 0, 0);
                        var distanceToCenter = RailPositionRouteDistanceFinder.FindShortestDistance(centerPoint, stationPoint);
                        // 中心点からの距離が探索半径外なら駅スナップ候補にしない
                        // Exclude nodes outside station-snap search radius from center
                        if (distanceToCenter < 0 || distanceToCenter >= nearestDistance)
                        {
                            continue;
                        }
                        nearestDistance = distanceToCenter;
                        nearestPoint = stationPoint;
                        hasCandidate = true;
                    }
                }
                return hasCandidate;
            }

            #endregion
        }

        // 日本語: 未到達経路のうち中心点に最も近いレール端をスナップ開始点として求める。
        // English: Resolve the nearest rail-end from unreached routes as the snap start point.
        internal static bool TryFindNearestRailEndSnapPoint(
            RailPosition centerRailPosition,
            IReadOnlyList<RailPathTracer.UnreachedRoute> frontUnreachedRoutes,
            IReadOnlyList<RailPathTracer.UnreachedRoute> rearUnreachedRoutes,
            out RailPosition snapStartPoint,
            out bool snapFromCenterForward)
        {
            snapStartPoint = null;
            snapFromCenterForward = true;
            if (centerRailPosition == null)
            {
                return false;
            }

            var centerForwardPoint = centerRailPosition.GetHeadRailPosition();
            var centerBackwardPoint = centerForwardPoint.DeepCopy();
            centerBackwardPoint.Reverse();

            var nearestReachedDistance = int.MaxValue;
            var nearestPoint = default(RailPosition);
            var nearestFromCenterForward = true;
            EvaluateUnreachedRoutes(frontUnreachedRoutes);
            EvaluateUnreachedRoutes(rearUnreachedRoutes);
            if (nearestPoint == null)
            {
                return false;
            }

            snapStartPoint = nearestPoint;
            snapFromCenterForward = nearestFromCenterForward;
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
                    if (!TryOrientPointTowardCenter(
                            edgePoint,
                            centerForwardPoint,
                            centerBackwardPoint,
                            out var orientedPoint,
                            out _,
                            out var isCenterForwardSide))
                    {
                        continue;
                    }
                    if (candidate.ReachedDistance >= nearestReachedDistance)
                    {
                        continue;
                    }
                    nearestReachedDistance = candidate.ReachedDistance;
                    nearestPoint = orientedPoint;
                    nearestFromCenterForward = isCenterForwardSide;
                }
            }

            #endregion
        }

        #region Internal

        private static bool TryOrientPointTowardCenter(
            RailPosition point,
            RailPosition centerForwardPoint,
            RailPosition centerBackwardPoint,
            out RailPosition orientedPoint,
            out int distanceToCenter,
            out bool isCenterForwardSide)
        {
            if (point == null || centerForwardPoint == null || centerBackwardPoint == null)
            {
                orientedPoint = null;
                distanceToCenter = int.MaxValue;
                isCenterForwardSide = true;
                return false;
            }

            var bestPoint = default(RailPosition);
            var bestDistance = int.MaxValue;
            var bestIsCenterForwardSide = true;
            Update(point);

            var reversed = point.DeepCopy();
            reversed.Reverse();
            Update(reversed);
            orientedPoint = bestPoint;
            distanceToCenter = bestDistance;
            isCenterForwardSide = bestIsCenterForwardSide;
            return bestPoint != null;

            #region Internal

            void Update(RailPosition candidate)
            {
                if (candidate == null)
                {
                    return;
                }
                if (!TryFindDistanceToCenter(candidate, centerForwardPoint, centerBackwardPoint, out var candidateDistance, out var candidateIsCenterForwardSide))
                {
                    return;
                }
                if (candidateDistance >= bestDistance)
                {
                    return;
                }
                bestPoint = candidate.DeepCopy();
                bestDistance = candidateDistance;
                bestIsCenterForwardSide = candidateIsCenterForwardSide;
            }

            #endregion
        }

        private static bool TryFindDistanceToCenter(
            RailPosition fromPoint,
            RailPosition centerForwardPoint,
            RailPosition centerBackwardPoint,
            out int distanceToCenter,
            out bool isCenterForwardSide)
        {
            distanceToCenter = int.MaxValue;
            isCenterForwardSide = true;
            var forwardDistance = RailPositionRouteDistanceFinder.FindShortestDistance(fromPoint, centerForwardPoint);
            var backwardDistance = RailPositionRouteDistanceFinder.FindShortestDistance(fromPoint, centerBackwardPoint);
            if (forwardDistance < 0 && backwardDistance < 0)
            {
                return false;
            }
            if (backwardDistance < 0 || (forwardDistance >= 0 && forwardDistance <= backwardDistance))
            {
                distanceToCenter = forwardDistance;
                isCenterForwardSide = true;
                return true;
            }
            distanceToCenter = backwardDistance;
            isCenterForwardSide = false;
            return true;
        }

        #endregion
    }
}
