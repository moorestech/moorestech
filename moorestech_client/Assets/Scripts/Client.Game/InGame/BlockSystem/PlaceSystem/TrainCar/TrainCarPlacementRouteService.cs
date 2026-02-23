using Game.Train.RailGraph;
using Game.Train.RailPositions;
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    internal sealed class TrainCarPlacementRouteService
    {
        private readonly RailPathTracer _pathTracer;

        internal TrainCarPlacementRouteService(RailPathTracer pathTracer)
        {
            _pathTracer = pathTracer;
        }

        // 日本語: 要件1。前後マージン探索から N'+M' の重複検査indexを構築する。
        // English: Requirement-1 helper that builds overlap index from margin-probe routes N'+M'.
        internal RailPositionOverlapDetector.OverlapIndex CreateRequirement1OverlapIndex(
            RailPosition centerRailPosition,
            int trainLength,
            int additionalMarginLength)
        {
            var requirement1OverlapProbeRoutes = new List<RailPosition>();
            var frontLengthWithMargin = (trainLength + 1) / 2 + additionalMarginLength;
            var rearLengthWithMargin = trainLength / 2 + additionalMarginLength;
            var requirement1FrontRoutes = new List<RailPosition>();
            var requirement1RearRoutes = new List<RailPosition>();
            var hasMarginRoute = TryBuildFrontRearRoutes(
                centerRailPosition,
                frontLengthWithMargin,
                rearLengthWithMargin,
                requirement1FrontRoutes,
                requirement1RearRoutes);
            if (hasMarginRoute)
            {
                requirement1OverlapProbeRoutes.AddRange(requirement1FrontRoutes);
                requirement1OverlapProbeRoutes.AddRange(requirement1RearRoutes);
            }

            return RailPositionOverlapDetector.CreateIndex(requirement1OverlapProbeRoutes);
        }

        // 日本語: 要件1。接続点から trainLength 分のS候補を前方DFSで再構築する。
        // English: Requirement-1 helper that rebuilds S candidates by forward DFS with trainLength.
        internal bool TryRebuildRequirement1SnapCandidates(
            RailPosition snapStartPoint,
            int trainLength,
            out List<RailPosition> routes,
            out int routeCount)
        {
            routes = new List<RailPosition>();
            routeCount = 0;
            if (snapStartPoint == null || trainLength <= 0)
            {
                return false;
            }

            var tracePoint = snapStartPoint.DeepCopy();
            tracePoint.Reverse();
            var traceStartPoint = tracePoint.GetHeadRailPosition();
            if (!_pathTracer.TryTraceForwardRoutesByDfs(traceStartPoint, trainLength, out var tracedRoutes) ||
                tracedRoutes == null ||
                tracedRoutes.Count <= 0)
            {
                return false;
            }

            routes.AddRange(tracedRoutes);
            routeCount = routes.Count;
            return routeCount > 0;
        }

        // 日本語: 要件2/3。スナップ開始点から trainLength 分の候補を前方DFSで列挙する。
        // English: Requirement-2/3 helper that enumerates train-length routes from snap start by forward DFS.
        internal bool TryBuildSnapRoutesFromPoint(RailPosition snapStartPoint, int trainLength, out List<RailPosition> routes)
        {
            routes = new List<RailPosition>();
            if (snapStartPoint == null || trainLength <= 0)
            {
                return false;
            }

            var traceStartPoint = snapStartPoint.GetHeadRailPosition();
            if (!_pathTracer.TryTraceForwardRoutesByDfs(traceStartPoint, trainLength, out var tracedRoutes) ||
                tracedRoutes == null ||
                tracedRoutes.Count <= 0)
            {
                return false;
            }

            routes.AddRange(tracedRoutes);
            return routes.Count > 0;
        }

        // 日本語: 要件3。centerから前後方向の未到達経路のみを収集する。
        // English: Requirement-3 helper that collects unreached routes in both directions from center.
        internal void CollectRequirement3UnreachedRoutes(
            RailPosition centerRailPosition,
            int frontLength,
            int rearLength,
            List<RailPathTracer.UnreachedRoute> frontUnreachedRoutes,
            List<RailPathTracer.UnreachedRoute> rearUnreachedRoutes)
        {
            frontUnreachedRoutes.Clear();
            rearUnreachedRoutes.Clear();
            if (centerRailPosition == null)
            {
                return;
            }

            var centerForwardPoint = centerRailPosition.DeepCopy();
            if (_pathTracer.TryTraceForwardUnreachedRoutesByDfs(centerForwardPoint, frontLength, out var tracedFrontUnreached) &&
                tracedFrontUnreached != null &&
                tracedFrontUnreached.Count > 0)
            {
                frontUnreachedRoutes.AddRange(tracedFrontUnreached);
            }

            var centerBackwardPoint = centerRailPosition.DeepCopy();
            centerBackwardPoint.Reverse();
            if (_pathTracer.TryTraceForwardUnreachedRoutesByDfs(centerBackwardPoint, rearLength, out var tracedRearUnreached) &&
                tracedRearUnreached != null &&
                tracedRearUnreached.Count > 0)
            {
                rearUnreachedRoutes.AddRange(tracedRearUnreached);
            }
        }

        // 日本語: 要件4。前後候補を毎フレーム再構築し、組み合わせ総数を返す。
        // English: Requirement-4 helper that rebuilds front/rear candidates and returns pair count.
        internal bool TryRebuildRequirement4SelectionCandidates(
            RailPosition centerRailPosition,
            int trainLength,
            out List<RailPosition> frontRoutes,
            out List<RailPosition> rearRoutesFromCenter,
            out int routePairCount)
        {
            frontRoutes = new List<RailPosition>();
            rearRoutesFromCenter = new List<RailPosition>();
            routePairCount = 0;

            var frontLength = (trainLength + 1) / 2;
            var rearLength = trainLength / 2;
            if (!TryBuildFrontRearRoutes(centerRailPosition, frontLength, rearLength, frontRoutes, rearRoutesFromCenter))
            {
                return false;
            }

            var pairCount = frontRoutes.Count * rearRoutesFromCenter.Count;
            routePairCount = pairCount > int.MaxValue ? int.MaxValue : pairCount;
            return routePairCount > 0;
        }

        // 日本語: 中心点から前後距離指定で候補経路を再構築する共通API。
        // English: Shared API that rebuilds front/rear candidate routes from center with explicit distances.
        internal bool TryBuildFrontRearRoutesFromCenter(
            RailPosition centerRailPosition,
            int frontLength,
            int rearLength,
            out List<RailPosition> frontRoutes,
            out List<RailPosition> rearRoutesFromCenter)
        {
            frontRoutes = new List<RailPosition>();
            rearRoutesFromCenter = new List<RailPosition>();
            return TryBuildFrontRearRoutes(centerRailPosition, frontLength, rearLength, frontRoutes, rearRoutesFromCenter);
        }

        // 日本語: 要件4。selectionStepに応じた (front,rear) ペアを選択して1本に結合する。
        // English: Requirement-4 helper that picks a (front,rear) pair by selectionStep and combines them.
        internal bool TryBuildRequirement4SelectedRailPosition(
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

        // 日本語: 要件1-4共通。候補経路から既存TrainUnit重複を除外する。
        // English: Shared helper for requirement 1-4 that filters routes overlapping existing train units.
        internal List<RailPosition> FilterRoutesWithoutOverlap(
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

        private bool TryBuildFrontRearRoutes(
            RailPosition centerRailPosition,
            int frontLength,
            int rearLength,
            List<RailPosition> frontRoutes,
            List<RailPosition> rearRoutesFromCenter)
        {
            frontRoutes.Clear();
            rearRoutesFromCenter.Clear();
            if (centerRailPosition == null)
            {
                return false;
            }

            var centerPoint = centerRailPosition.DeepCopy();
            if (!_pathTracer.TryTraceForwardRoutesByDfs(centerPoint, frontLength, out var tracedFrontRoutes) ||
                tracedFrontRoutes == null ||
                tracedFrontRoutes.Count <= 0)
            {
                return false;
            }
            frontRoutes.AddRange(tracedFrontRoutes);

            var reversedCenterPoint = centerPoint.DeepCopy();
            reversedCenterPoint.Reverse();
            if (!_pathTracer.TryTraceForwardRoutesByDfs(reversedCenterPoint, rearLength, out var tracedRearRoutesReversed) ||
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
