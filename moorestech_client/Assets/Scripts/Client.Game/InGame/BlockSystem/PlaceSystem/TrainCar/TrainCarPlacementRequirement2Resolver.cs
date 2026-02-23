using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal sealed class TrainCarPlacementRequirement2Resolver
    {
        private readonly TrainCarPlacementRouteService _routeService;
        private readonly TrainCarPlacementSnapPointFinder _snapPointFinder;

        internal TrainCarPlacementRequirement2Resolver(
            TrainCarPlacementRouteService routeService,
            TrainCarPlacementSnapPointFinder snapPointFinder)
        {
            _routeService = routeService;
            _snapPointFinder = snapPointFinder;
        }

        // 日本語: 要件2。駅nodeスナップ候補T'を構築し、向き基準情報を返す。
        // English: Requirement-2 resolver that builds station snap candidates T' and returns facing baseline.
        internal bool TryResolve(
            RailPosition centerRailPosition,
            int trainLength,
            RailPositionOverlapDetector.OverlapIndex allTrainUnitOverlapIndex,
            out List<RailPosition> requirement2Routes,
            out bool snapFromCenterForward)
        {
            requirement2Routes = new List<RailPosition>();
            snapFromCenterForward = true;
            if (centerRailPosition == null || trainLength <= 0)
            {
                return false;
            }

            var frontLength = (trainLength + 1) / 2;
            var rearLength = trainLength / 2;
            if (!_routeService.TryBuildFrontRearRoutesFromCenter(
                    centerRailPosition,
                    frontLength,
                    rearLength,
                    out var frontRoutes,
                    out var rearRoutesFromCenter))
            {
                return false;
            }
            if (!_snapPointFinder.TryFindRequirement2NearestStationSnapPoint(
                    centerRailPosition,
                    frontRoutes,
                    rearRoutesFromCenter,
                    frontLength,
                    rearLength,
                    out var snapStartPoint,
                    out var requirement2SnapFromCenterForward))
            {
                return false;
            }
            if (!_routeService.TryBuildSnapRoutesFromPoint(snapStartPoint, trainLength, out var tracedRoutes))
            {
                return false;
            }

            var filteredRoutes = _routeService.FilterRoutesWithoutOverlap(tracedRoutes, allTrainUnitOverlapIndex);
            if (filteredRoutes.Count <= 0)
            {
                return false;
            }

            requirement2Routes.AddRange(filteredRoutes);
            snapFromCenterForward = requirement2SnapFromCenterForward;
            return true;
        }
    }
}
