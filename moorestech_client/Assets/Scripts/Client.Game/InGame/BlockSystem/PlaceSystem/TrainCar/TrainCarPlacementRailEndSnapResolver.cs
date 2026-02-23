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

            if (!TrainCarPlacementSnapPointFinder.TryFindNearestRailEndSnapPoint(
                    centerRailPosition,
                    frontUnreachedRoutes,
                    rearUnreachedRoutes,
                    out var snapStartPoint,
                    out var railEndSnapFromCenterForward))
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

            railEndSnapRoutes.AddRange(filteredRoutes);
            snapFromCenterForward = railEndSnapFromCenterForward;
            return true;
        }
    }
}
