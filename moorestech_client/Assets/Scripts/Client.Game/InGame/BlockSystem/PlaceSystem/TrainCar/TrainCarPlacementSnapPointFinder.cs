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
    }
}
