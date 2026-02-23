using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal sealed class TrainCarPlacementRequirement3Resolver
    {
        private readonly TrainCarPlacementRouteService _routeService;
        private readonly TrainCarPlacementSnapPointFinder _snapPointFinder;

        internal TrainCarPlacementRequirement3Resolver(
            TrainCarPlacementRouteService routeService,
            TrainCarPlacementSnapPointFinder snapPointFinder)
        {
            _routeService = routeService;
            _snapPointFinder = snapPointFinder;
        }

        // 日本語: 要件3。レール端スナップ候補U'を構築し、向き基準情報を返す。
        // English: Requirement-3 resolver that builds rail-end snap candidates U' and returns facing baseline.
        internal bool TryResolve(
            RailPosition centerRailPosition,
            int trainLength,
            RailPositionOverlapDetector.OverlapIndex allTrainUnitOverlapIndex,
            out List<RailPosition> requirement3Routes,
            out bool snapFromCenterForward)
        {
            requirement3Routes = new List<RailPosition>();
            snapFromCenterForward = true;
            if (centerRailPosition == null || trainLength <= 0)
            {
                return false;
            }

            var frontLength = (trainLength + 1) / 2;
            var rearLength = trainLength / 2;
            var frontUnreachedRoutes = new List<RailPathTracer.UnreachedRoute>();
            var rearUnreachedRoutes = new List<RailPathTracer.UnreachedRoute>();
            _routeService.CollectRequirement3UnreachedRoutes(
                centerRailPosition,
                frontLength,
                rearLength,
                frontUnreachedRoutes,
                rearUnreachedRoutes);

            if (!_snapPointFinder.TryFindRequirement3NearestRailEndSnapPoint(
                    centerRailPosition,
                    frontUnreachedRoutes,
                    rearUnreachedRoutes,
                    out var snapStartPoint,
                    out var requirement3SnapFromCenterForward))
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

            requirement3Routes.AddRange(filteredRoutes);
            snapFromCenterForward = requirement3SnapFromCenterForward;
            return true;
        }
    }
}
