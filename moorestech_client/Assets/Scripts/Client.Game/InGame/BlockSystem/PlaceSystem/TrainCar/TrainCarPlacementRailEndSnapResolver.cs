using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarPlacementRailEndSnapResolver
    {
        // 日本語: レール端スナップ候補U'を構築し、向き基準情報を返す。
        // English: builds rail-end snap candidates U' and returns facing baseline.
        internal static bool TryResolveRailEndSnapRoutes(
            RailPosition centerRailPosition,
            int trainLength,
            RailPathTracer pathTracer,
            RailPositionOverlapDetector.OverlapIndex allTrainUnitOverlapIndex,
            out List<RailPosition> railEndSnapRoutes,
            out bool snapFromCenterForward)
        {
            railEndSnapRoutes = new List<RailPosition>();
            snapFromCenterForward = true;
            if (centerRailPosition == null || trainLength <= 0 || pathTracer == null)
            {
                return false;
            }

            var frontLength = (trainLength + 1) / 2;
            var rearLength = trainLength / 2;
            var frontUnreachedRoutes = new List<RailPathTracer.UnreachedRoute>();
            var rearUnreachedRoutes = new List<RailPathTracer.UnreachedRoute>();
            TrainCarPlacementRouteService.CollectRailEndSnapUnreachedRoutes(
                centerRailPosition,
                frontLength,
                rearLength,
                pathTracer,
                frontUnreachedRoutes,
                rearUnreachedRoutes);
            
            
            // 日本語: 未到達経路のうち中心点に最も近いレール端をスナップ開始点として求める。
            // English: Resolve the nearest rail-end from unreached routes as the snap start point.
            // frontUnreachedRoutesの中で一番距離が短いものを抽出
            // If there are unreached routes, extract the one with the shortest distance in frontUnreachedRoutes.
            int minDistance = int.MaxValue;
            RailPosition snapStartPoint = null;
            foreach (var route in frontUnreachedRoutes)
            {
                if (route.ReachedDistance < minDistance)
                {
                    minDistance = route.ReachedDistance;
                    snapStartPoint = route.Route;
                    snapFromCenterForward = true;
                }
            }
            foreach (var route in rearUnreachedRoutes)
            {
                if (route.ReachedDistance < minDistance)
                {
                    minDistance = route.ReachedDistance;
                    snapStartPoint = route.Route;
                    snapFromCenterForward = false;
                }
            }
            
            if (snapStartPoint == null)
            {
                return false;
            }
            
            snapStartPoint = snapStartPoint.GetHeadRailPosition();
            snapStartPoint.Reverse();
            
            if (!pathTracer.TryTraceForwardRoutesByDfs(snapStartPoint, trainLength, out var tracedRoutes))
            {
                return false;
            }

            var filteredRoutes = TrainCarPlacementRouteService.FilterRoutesWithoutOverlap(tracedRoutes, allTrainUnitOverlapIndex);
            if (filteredRoutes.Count <= 0)
            {
                return false;
            }

            railEndSnapRoutes.AddRange(filteredRoutes);
            return true;
        }
    }
}
