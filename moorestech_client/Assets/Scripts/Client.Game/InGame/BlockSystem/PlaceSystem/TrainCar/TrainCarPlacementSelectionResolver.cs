using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarPlacementSelectionResolver
    {
        // 日本語: 既存編成接続スナップ候補から selectionStep に応じた1本を選択する。
        // English: Select one route from attach-snap candidates by selectionStep.
        internal static bool TryBuildSelectedAttachSnapRoute(
            IReadOnlyList<RailPosition> routes,
            int routeCount,
            int selectionStep,
            bool attachCarFacingForward,
            out RailPosition resolvedRailPosition)
        {
            resolvedRailPosition = null;
            if (!TryResolveRouteIndex(routes, routeCount, selectionStep, out var routeIndex, out _))
            {
                return false;
            }

            var selectedRoute = routes[routeIndex]?.DeepCopy();
            if (selectedRoute == null)
            {
                return false;
            }
            if (attachCarFacingForward)
            {
                selectedRoute.Reverse();
            }

            resolvedRailPosition = selectedRoute;
            return true;
        }

        // 日本語: 駅/レール端スナップ候補から、最短snapの前後情報を基準に1本選択する。
        // English: Select one route from station/rail-end snap candidates using nearest-snap side for orientation.
        internal static bool TryBuildCreateModeSelectedSingleRoute(
            IReadOnlyList<RailPosition> routes,
            int routeCount,
            int selectionStep,
            bool snapFromCenterForward,
            out RailPosition resolvedRailPosition)
        {
            resolvedRailPosition = null;
            if (!TryResolveRouteIndex(routes, routeCount, selectionStep, out var routeIndex, out _))
            {
                return false;
            }

            var selectedRoute = routes[routeIndex]?.DeepCopy();
            if (selectedRoute == null)
            {
                return false;
            }

            // 日本語: 駅/レール端スナップの反転は最短snap側の基準向きのみで決める。
            // English: Station/rail-end snap reversal is determined only by nearest-snap baseline.
            // 日本語: Rキー偶奇による全体反転は Detector 冒頭の centerRailPosition.Reverse() で先に適用済み。
            // English: R-key parity flip is already applied earlier by centerRailPosition.Reverse() in Detector.
            var shouldReverse = snapFromCenterForward;
            if (shouldReverse)
            {
                selectedRoute.Reverse();
            }

            resolvedRailPosition = selectedRoute;
            return true;
        }

        #region Internal

        private static bool TryResolveRouteIndex(
            IReadOnlyList<RailPosition> routes,
            int routeCount,
            int selectionStep,
            out int routeIndex,
            out int normalizedStep)
        {
            routeIndex = 0;
            normalizedStep = 0;
            if (routes == null || routeCount <= 0 || routes.Count <= 0)
            {
                return false;
            }

            var totalStateCount = routeCount * 2;
            if (totalStateCount <= 0)
            {
                return false;
            }

            normalizedStep = selectionStep % totalStateCount;
            routeIndex = normalizedStep / 2;
            return routeIndex >= 0 && routeIndex < routes.Count;
        }

        #endregion
    }
}
