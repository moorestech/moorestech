using Game.Train.RailPositions;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarPlacementSelectionResolver
    {
        // 日本語: 駅/レール端スナップ候補から、最短snapの前後情報を基準に1本選択する。
        // English: Select one route from station/rail-end snap candidates using nearest-snap side for orientation.
        internal static bool TrySelectSingleRoute(
            IReadOnlyList<RailPosition> routes,
            int selectionStep,
            bool snapFromCenterForward,
            out RailPosition resolvedRailPosition)
        {
            var totalStateCount = routes.Count * 2;
            var routeIndex = selectionStep % totalStateCount / 2;
            
            resolvedRailPosition = routes[routeIndex];
            if (resolvedRailPosition == null)
            {
                return false;
            }
            resolvedRailPosition = resolvedRailPosition.DeepCopy();
            if (snapFromCenterForward)
            {
                resolvedRailPosition.Reverse();
            }
            return true;
        }
    }
}
