using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal static class TrainCarPlacementRouteService
    {
        // 日本語: 前後マージン探索から N'+M' の重複検査indexを構築する。
        // English: builds overlap index from margin-probe routes N'+M'.
        internal static RailPositionOverlapDetector.OverlapIndex CreateAttachProbeOverlapIndex(
            RailPosition centerRailPosition,
            int trainLength,
            int additionalMarginLength,
            RailPathTracer pathTracer)
        {
            var attachProbeRoutes = new List<RailPosition>();
            var frontLengthWithMargin = (trainLength + 1) / 2 + additionalMarginLength;
            var rearLengthWithMargin = trainLength / 2 + additionalMarginLength;
            var frontProbeRoutes = new List<RailPosition>();
            var rearProbeRoutes = new List<RailPosition>();
            var hasMarginRoute = TryBuildFrontRearRoutes(
                centerRailPosition,
                frontLengthWithMargin,
                rearLengthWithMargin,
                pathTracer,
                frontProbeRoutes,
                rearProbeRoutes);
            if (hasMarginRoute)
            {
                attachProbeRoutes.AddRange(frontProbeRoutes);
                attachProbeRoutes.AddRange(rearProbeRoutes);
            }

            return RailPositionOverlapDetector.CreateIndex(attachProbeRoutes);
        }

        // 日本語: 既存編成接続スナップ用に、接続点から trainLength 分の候補を前方DFSで再構築する。
        // English: Rebuild attach-snap candidates from the attach point by forward DFS with trainLength.
        internal static bool TryBuildAttachSnapCandidates(
            RailPosition snapStartPoint,
            int trainLength,
            RailPathTracer pathTracer,
            out List<RailPosition> routes,
            out int routeCount)
        {
            routes = new List<RailPosition>();
            routeCount = 0;
            if (snapStartPoint == null || trainLength <= 0 || pathTracer == null)
            {
                return false;
            }

            var tracePoint = snapStartPoint.DeepCopy();
            tracePoint.Reverse();
            var traceStartPoint = tracePoint.GetHeadRailPosition();
            if (!pathTracer.TryTraceForwardRoutesByDfs(traceStartPoint, trainLength, out var tracedRoutes) ||
                tracedRoutes == null ||
                tracedRoutes.Count <= 0)
            {
                return false;
            }

            routes.AddRange(tracedRoutes);
            routeCount = routes.Count;
            return routeCount > 0;
        }

        // 日本語: 駅/レール端スナップ用に、開始点から trainLength 分の候補を前方DFSで列挙する。
        // English: Enumerate station/rail-end snap routes from snap start by forward DFS with trainLength.
        internal static bool TryBuildSnapRoutesFromPoint(
            RailPosition snapStartPoint,
            int trainLength,
            RailPathTracer pathTracer,
            out List<RailPosition> routes)
        {
            routes = new List<RailPosition>();
            if (snapStartPoint == null || trainLength <= 0 || pathTracer == null)
            {
                return false;
            }

            var traceStartPoint = snapStartPoint.GetHeadRailPosition();
            if (!pathTracer.TryTraceForwardRoutesByDfs(traceStartPoint, trainLength, out var tracedRoutes) ||
                tracedRoutes == null ||
                tracedRoutes.Count <= 0)
            {
                return false;
            }

            routes.AddRange(tracedRoutes);
            return routes.Count > 0;
        }

        // 日本語: レール端スナップ用に、centerから前後方向の未到達経路のみを収集する。
        // English: Collect unreached routes in both directions from center for rail-end snap.
        internal static void CollectRailEndSnapUnreachedRoutes(
            RailPosition centerRailPosition,
            int frontLength,
            int rearLength,
            RailPathTracer pathTracer,
            List<RailPathTracer.UnreachedRoute> frontUnreachedRoutes,
            List<RailPathTracer.UnreachedRoute> rearUnreachedRoutes)
        {
            frontUnreachedRoutes.Clear();
            rearUnreachedRoutes.Clear();
            if (centerRailPosition == null || pathTracer == null)
            {
                return;
            }

            var centerForwardPoint = centerRailPosition.DeepCopy();
            if (pathTracer.TryTraceForwardUnreachedRoutesByDfs(centerForwardPoint, frontLength, out var tracedFrontUnreached) &&
                tracedFrontUnreached != null &&
                tracedFrontUnreached.Count > 0)
            {
                frontUnreachedRoutes.AddRange(tracedFrontUnreached);
            }

            var centerBackwardPoint = centerRailPosition.DeepCopy();
            centerBackwardPoint.Reverse();
            if (pathTracer.TryTraceForwardUnreachedRoutesByDfs(centerBackwardPoint, rearLength, out var tracedRearUnreached) &&
                tracedRearUnreached != null &&
                tracedRearUnreached.Count > 0)
            {
                rearUnreachedRoutes.AddRange(tracedRearUnreached);
            }
        }

        // 日本語: 通常配置用に、前後候補を毎フレーム再構築し、組み合わせ総数を返す。
        // English: Rebuild front/rear candidates for normal placement and return the pair count.
        internal static bool TryBuildCarPlacementSelectionCandidates(
            RailPosition centerRailPosition,
            int trainLength,
            RailPathTracer pathTracer,
            out List<RailPosition> frontRoutes,
            out List<RailPosition> rearRoutesFromCenter,
            out int routePairCount)
        {
            frontRoutes = new List<RailPosition>();
            rearRoutesFromCenter = new List<RailPosition>();
            routePairCount = 0;

            var frontLength = (trainLength + 1) / 2;
            var rearLength = trainLength / 2;
            if (!TryBuildFrontRearRoutes(centerRailPosition, frontLength, rearLength, pathTracer, frontRoutes, rearRoutesFromCenter))
            {
                return false;
            }

            var pairCount = frontRoutes.Count * rearRoutesFromCenter.Count;
            routePairCount = pairCount > int.MaxValue ? int.MaxValue : pairCount;
            return routePairCount > 0;
        }

        // 日本語: 中心点から前後距離指定で候補経路を再構築する共通API。
        // English: Shared API that rebuilds front/rear candidate routes from center with explicit distances.
        internal static bool TryBuildFrontRearRoutesFromCenter(
            RailPosition centerRailPosition,
            int frontLength,
            int rearLength,
            RailPathTracer pathTracer,
            out List<RailPosition> frontRoutes,
            out List<RailPosition> rearRoutesFromCenter)
        {
            frontRoutes = new List<RailPosition>();
            rearRoutesFromCenter = new List<RailPosition>();
            return TryBuildFrontRearRoutes(centerRailPosition, frontLength, rearLength, pathTracer, frontRoutes, rearRoutesFromCenter);
        }

        // 日本語: 通常配置用に、selectionStepに応じた (front,rear) ペアを選択して1本に結合する。
        // English: Pick a (front,rear) pair for normal placement by selectionStep and combine into one route.
        internal static bool TryBuildSelectedCarPlacement(
            IReadOnlyList<RailPosition> frontRoutes,
            IReadOnlyList<RailPosition> rearRoutesFromCenter,
            int routePairCount,
            int selectionStep,
            out RailPosition resolvedRailPosition)
        {
            resolvedRailPosition = null;
            if (routePairCount <= 0 || frontRoutes.Count <= 0 || rearRoutesFromCenter.Count <= 0)
            {
                return false;
            }

            var totalStateCount = routePairCount * 2;
            if (totalStateCount <= 0)
            {
                return false;
            }

            var normalizedStep = selectionStep % totalStateCount;
            var routePairIndex = normalizedStep / 2;
            var rearCount = rearRoutesFromCenter.Count;
            var frontIndex = routePairIndex / rearCount;
            var rearIndex = routePairIndex % rearCount;
            if (frontIndex >= frontRoutes.Count || rearIndex >= rearRoutesFromCenter.Count)
            {
                return false;
            }

            return TryCombineRoutes(frontRoutes[frontIndex], rearRoutesFromCenter[rearIndex], out resolvedRailPosition);
        }

        // 日本語: 全配置モード共通で、候補経路から既存TrainUnit重複を除外する。
        // English: Shared helper that filters candidate routes overlapping existing train units across all modes.
        internal static List<RailPosition> FilterRoutesWithoutOverlap(
            IReadOnlyList<RailPosition> candidateRoutes,
            RailPositionOverlapDetector.OverlapIndex allTrainUnitOverlapIndex)
        {
            var filteredRoutes = new List<RailPosition>();
            if (candidateRoutes == null || candidateRoutes.Count <= 0)
            {
                return filteredRoutes;
            }

            for (var i = 0; i < candidateRoutes.Count; i++)
            {
                var candidate = candidateRoutes[i];
                if (candidate == null)
                {
                    continue;
                }
                if (allTrainUnitOverlapIndex != null && RailPositionOverlapDetector.HasOverlap(candidate, allTrainUnitOverlapIndex))
                {
                    continue;
                }
                filteredRoutes.Add(candidate);
            }

            return filteredRoutes;
        }

        #region Internal

        private static bool TryBuildFrontRearRoutes(
            RailPosition centerRailPosition,
            int frontLength,
            int rearLength,
            RailPathTracer pathTracer,
            List<RailPosition> frontRoutes,
            List<RailPosition> rearRoutesFromCenter)
        {
            frontRoutes.Clear();
            rearRoutesFromCenter.Clear();
            if (centerRailPosition == null || pathTracer == null)
            {
                return false;
            }

            var centerPoint = centerRailPosition.DeepCopy();
            if (!pathTracer.TryTraceForwardRoutesByDfs(centerPoint, frontLength, out var tracedFrontRoutes) ||
                tracedFrontRoutes == null ||
                tracedFrontRoutes.Count <= 0)
            {
                return false;
            }
            frontRoutes.AddRange(tracedFrontRoutes);

            var reversedCenterPoint = centerPoint.DeepCopy();
            reversedCenterPoint.Reverse();
            if (!pathTracer.TryTraceForwardRoutesByDfs(reversedCenterPoint, rearLength, out var tracedRearRoutesReversed) ||
                tracedRearRoutesReversed == null ||
                tracedRearRoutesReversed.Count <= 0)
            {
                return false;
            }

            for (var i = 0; i < tracedRearRoutesReversed.Count; i++)
            {
                var route = tracedRearRoutesReversed[i]?.DeepCopy();
                if (route == null)
                {
                    continue;
                }
                route.Reverse();
                rearRoutesFromCenter.Add(route);
            }

            return rearRoutesFromCenter.Count > 0;
        }

        private static bool TryCombineRoutes(RailPosition frontRoute, RailPosition rearRouteFromCenter, out RailPosition combinedRoute)
        {
            combinedRoute = null;
            if (frontRoute == null || rearRouteFromCenter == null)
            {
                return false;
            }

            var frontRearPoint = frontRoute.GetRearRailPosition();
            var rearHeadPoint = rearRouteFromCenter.GetHeadRailPosition();
            if (!frontRearPoint.IsSamePositionAllowNodeOverlap(rearHeadPoint))
            {
                return false;
            }

            combinedRoute = frontRoute.DeepCopy();
            combinedRoute.AppendRailPositionAtRear(rearRouteFromCenter.DeepCopy());
            return true;
        }

        #endregion
    }
}
