using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarPlacementStationSnapResolver
    {
        // 日本語: 駅nodeスナップ候補を構築し、向き基準情報を返す。
        // English: Build station snap candidates and return the facing baseline.
        internal static bool TryResolveStationSnapRoutes(
            RailPosition centerRailPosition,
            int trainLength,
            RailPathTracer pathTracer,
            RailPositionOverlapDetector.OverlapIndex allTrainUnitOverlapIndex,
            out List<RailPosition> stationSnapRoutes,
            out bool snapFromCenterForward)
        {
            stationSnapRoutes = new List<RailPosition>();
            snapFromCenterForward = true;
            if (centerRailPosition == null || trainLength <= 0 || pathTracer == null)
            {
                return false;
            }

            var frontLength = (trainLength + 1) / 2;
            var rearLength = trainLength / 2;
            var frontRoutes = new List<RailPosition>();
            var rearRoutes = new List<RailPosition>();
            if (!TrainCarPlacementRouteService.TryBuildFrontRearRoutes(
                    centerRailPosition,
                    frontLength,
                    rearLength,
                    pathTracer,
                    frontRoutes,
                    rearRoutes))
            {
                return false;
            }
            if (!TrainCarPlacementSnapPointFinder.TryFindNearestStationSnapPoint(
                    centerRailPosition,
                    frontRoutes,
                    rearRoutes,
                    frontLength,
                    rearLength,
                    out var snapStartPoint,
                    out var stationSnapFromCenterForward))
            {
                return false;
            }
            if (!TrainCarPlacementRouteService.TryBuildSnapRoutesFromPoint(snapStartPoint, trainLength, pathTracer, out var tracedRoutes))
            {
                return false;
            }

            var filteredRoutes = TrainCarPlacementRouteService.FilterRoutesWithoutOverlap(tracedRoutes, allTrainUnitOverlapIndex);
            if (filteredRoutes.Count <= 0)
            {
                return false;
            }

            stationSnapRoutes.AddRange(filteredRoutes);
            snapFromCenterForward = stationSnapFromCenterForward;
            return true;
        }
    }
}
