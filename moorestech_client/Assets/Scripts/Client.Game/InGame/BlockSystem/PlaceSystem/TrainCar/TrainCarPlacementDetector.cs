using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Core.Master;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Client.Common.LayerConst;

/// <summary>
/// 要件1,既存のtrainunitの先頭or最後尾に自動スナップする機能
/// 要件2,駅nodeに自動スナップする機能
/// 要件3,レールの端に自動スナップする機能
/// 要件4,通常のレール上に設置する機能
/// 
/// 詳細
/// 要件1
/// レイキャストのrailpositionから前方後方にDFSをする。距離はtrainCar.Length/2にマージンを足した距離(ちょっと先でもスナップするように)
/// 前方N`個、後方M`個のrailposition候補ができる。これら(N`+M`)と既存のtrainunit全体のRailPositionの重複を検査
/// 1つでも重複していたらどのTrainUnitか再調査、してなければ要件2へ
/// 重複している中で一番近いTrainUnitを1つ抽出。その点にスナップする



namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public interface ITrainCarPlacementDetector
    {
        bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit);
        void AdvanceSelection();
        void ResetSelection();
    }
    
    public class TrainCarPlacementDetector : ITrainCarPlacementDetector
    {
        private const int Requirement1AdditionalMarginLength = 256;
        private readonly Camera _mainCamera;
        private readonly TrainUnitClientCache _trainUnitCache;
        private readonly TrainCarCurveHitDistanceResolver _curveHitDistanceResolver;
        private readonly TrainCarCenterRailPositionResolver _centerRailPositionResolver;
        private readonly RailPathTracer _pathTracer;
        private int _routePairCount;
        private int _selectionStep;
        
        public TrainCarPlacementDetector(Camera mainCamera, RailGraphClientCache cache, TrainUnitClientCache trainUnitCache)
        {
            _mainCamera = mainCamera;
            _trainUnitCache = trainUnitCache;
            _pathTracer = new RailPathTracer(cache);
            _curveHitDistanceResolver = new TrainCarCurveHitDistanceResolver();
            _centerRailPositionResolver = new TrainCarCenterRailPositionResolver(cache, _curveHitDistanceResolver);
        }

        public void AdvanceSelection()
        {
            // 候補総数に応じて次の状態へ進める
            // Advance to the next state within current candidate count
            var totalStateCount = _routePairCount * 2;
            if (totalStateCount <= 0)
            {
                return;
            }
            _selectionStep = (_selectionStep + 1) % totalStateCount;
        }

        public void ResetSelection()
        {
            // 候補と選択状態を初期化する
            // Reset candidates and selection state
            _selectionStep = 0;
            _routePairCount = 0;
        }

        public bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit)
        {
            hit = default;
            // 車両マスターを解決する
            // Resolve the train car master definition
            if (!TryResolveTrainCarMaster(holdingItemId, out var trainCarMaster))
            {
                return false;
            }

            // レールID付きコライダーをレイキャストで取得する
            // Raycast to obtain the rail collider that carries the object id
            if (!PlaceSystemUtil.TryGetRaySpecifiedComponentHitPosition<RailObjectIdCarrier>(_mainCamera, out var hitPosition, out var railCarrier, Without_Player_MapObject_BlockBoundingBox_LayerMask))
            {
                return false;
            }

            // 位置からレールスナップショットを組み立てる
            // Build the rail snapshot from the hit position
            if (!TryBuildPlacement(hitPosition, railCarrier, trainCarMaster, out hit))
            {
                return false;
            }

            return true;

            #region Internal

            bool TryResolveTrainCarMaster(ItemId itemId, out TrainCarMasterElement trainCarMasterElement)
            {
                // 手持ちアイテムが車両マスターに対応するか判定する
                // Ensure the held item represents a train car master
                return MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(itemId, out trainCarMasterElement);
            }

            bool TryBuildPlacement(Vector3 hitPos, RailObjectIdCarrier railCarrier, TrainCarMasterElement trainCarMasterElement, out TrainCarPlacementHit result)
            {
                result = default;
                // 列車長とレール位置スナップショットを組み立てる
                // Compose train length and rail position snapshot
                var trainLength = TrainLengthConverter.ToRailUnits(trainCarMasterElement.Length);
                var isPlaceable = TryBuildRailPosition(
                    hitPos,
                    railCarrier,
                    trainLength,
                    out var railPosition,
                    out var overlapTrainInstanceIds,
                    out var placementMode,
                    out var targetTrainInstanceId,
                    out var attachCarFacingForward);
                result = new TrainCarPlacementHit(
                    isPlaceable,
                    railPosition,
                    overlapTrainInstanceIds,
                    placementMode,
                    targetTrainInstanceId,
                    attachCarFacingForward);
                return true;

                #region Internal

                bool TryBuildRailPosition(
                    Vector3 hitPosition,
                    RailObjectIdCarrier carrier,
                    int trainLength,
                    out RailPosition railPosition,
                    out IReadOnlyList<TrainInstanceId> overlapTrainInstanceIds,
                    out TrainCarPlacementMode placementMode,
                    out TrainInstanceId targetTrainInstanceId,
                    out bool attachCarFacingForward)
                {
                    railPosition = null;
                    overlapTrainInstanceIds = Array.Empty<TrainInstanceId>();
                    placementMode = TrainCarPlacementMode.CreateNewTrainUnit;
                    targetTrainInstanceId = TrainInstanceId.Empty;
                    attachCarFacingForward = true;
                    // 入力を検証し、ヒット点の中心RailPositionを解決する
                    // Validate inputs and resolve center RailPosition from hit point
                    if (carrier == null || trainLength <= 0)
                    {
                        return false;
                    }
                    if (!_centerRailPositionResolver.TryResolveCenterRailPosition(hitPosition, carrier, out var centerRailPosition))
                    {
                        return false;
                    }

                    // 要件1: N'+M'候補と既存TrainUnit全体の重複を抽出する
                    // Requirement 1: detect overlaps between N'+M' candidates and existing train units
                    var requirement1OverlapIndex = CreateRequirement1OverlapIndex(centerRailPosition, trainLength);
                    overlapTrainInstanceIds = ResolveOverlapTrainUnitsForRequirement1(
                        centerRailPosition,
                        requirement1OverlapIndex,
                        trainLength,
                        out var requirement1SnapStartPoint,
                        out var requirement1TargetTrainInstanceId,
                        out var requirement1AttachFacingForward);

                    // 要件1: 最短TrainUnitの接続点からS候補を作り、2S(反転込み)で選択する
                    // Requirement 1: build S routes from nearest unit endpoint and select within 2S(with reverse)
                    if (requirement1SnapStartPoint != null && requirement1TargetTrainInstanceId != TrainInstanceId.Empty)
                    {
                        _routePairCount = 0;
                        if (TryRebuildRequirement1SnapCandidates(requirement1SnapStartPoint, trainLength, out var requirement1Routes, out var requirement1RouteCount))
                        {
                            _routePairCount = requirement1RouteCount;
                            if (TryBuildSelectedSingleRoute(requirement1Routes, requirement1RouteCount, out railPosition, out var reverseSelected))
                            {
                                placementMode = TrainCarPlacementMode.AttachToExistingTrainUnit;
                                targetTrainInstanceId = requirement1TargetTrainInstanceId;
                                // 最短候補で使ったcenter前後向きを基準にし、R反転時は向きも反転させる
                                // Base facing uses nearest center direction and flips when R-reverse is selected
                                attachCarFacingForward = reverseSelected ? !requirement1AttachFacingForward : requirement1AttachFacingForward;
                                return true;
                            }
                        }
                    }

                    // 要件2: 未実装
                    // Requirement 2: not implemented yet
                    // TODO: 要件2の配置判定ロジックを実装する
                    // TODO: Implement requirement-2 placement logic

                    // 要件3: 未実装
                    // Requirement 3: not implemented yet
                    // TODO: 要件3の配置判定ロジックを実装する
                    // TODO: Implement requirement-3 placement logic

                    // 要件4候補（前輪側/後輪側）を毎フレーム再構築する
                    // Rebuild requirement-4 candidates (front/rear) every frame
                    _routePairCount = 0;
                    if (!TryRebuildSelectionCandidates(centerRailPosition, trainLength, out var frontRoutes, out var rearRoutesFromCenter, out var routePairCount))
                    {
                        return false;
                    }
                    _routePairCount = routePairCount;

                    // 要件4: 現在のインデックス選択経路を利用する
                    // Requirement 4: use the currently indexed route
                    // 現在の選択状態(経路+反転)から最終RailPositionを構築する
                    // Build final RailPosition from current route/reverse selection
                    return TryBuildSelectedRailPosition(frontRoutes, rearRoutesFromCenter, routePairCount, out railPosition);
                }

                bool TryBuildSelectedRailPosition(IReadOnlyList<RailPosition> frontRoutes, IReadOnlyList<RailPosition> rearRoutesFromCenter, int routePairCount, out RailPosition resolvedRailPosition)
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

                    var normalizedStep = _selectionStep % totalStateCount;
                    if (normalizedStep < 0)
                    {
                        normalizedStep += totalStateCount;
                    }

                    var reverseSelected = (normalizedStep & 1) == 1;
                    var routePairIndex = normalizedStep / 2;
                    var rearCount = rearRoutesFromCenter.Count;
                    var frontIndex = routePairIndex / rearCount;
                    var rearIndex = routePairIndex % rearCount;
                    if (frontIndex >= frontRoutes.Count || rearIndex >= rearRoutesFromCenter.Count)
                    {
                        return false;
                    }

                    return TryCombineRoutes(frontRoutes[frontIndex], rearRoutesFromCenter[rearIndex], reverseSelected, out resolvedRailPosition);
                }

                bool TryBuildSelectedSingleRoute(IReadOnlyList<RailPosition> routes, int routeCount, out RailPosition resolvedRailPosition, out bool reverseSelected)
                {
                    resolvedRailPosition = null;
                    reverseSelected = false;
                    if (routes == null || routeCount <= 0 || routes.Count <= 0)
                    {
                        return false;
                    }

                    var totalStateCount = routeCount * 2;
                    if (totalStateCount <= 0)
                    {
                        return false;
                    }

                    // Rキーは「反転優先」で1ステップ進むため、奇数ステップを反転に割り当てる
                    // R-key advances by reverse-priority, so odd steps are mapped to reverse selection
                    var normalizedStep = _selectionStep % totalStateCount;
                    if (normalizedStep < 0)
                    {
                        normalizedStep += totalStateCount;
                    }

                    reverseSelected = (normalizedStep & 1) == 1;
                    var routeIndex = normalizedStep / 2;
                    if (routeIndex < 0 || routeIndex >= routes.Count)
                    {
                        return false;
                    }

                    // S候補から1本を選び、必要ならRailPositionを反転して2S状態を表現する
                    // Pick one route from S and reverse it when needed to represent 2S states
                    var selectedRoute = routes[routeIndex]?.DeepCopy();
                    if (selectedRoute == null)
                    {
                        return false;
                    }
                    if (reverseSelected)
                    {
                        selectedRoute.Reverse();
                    }
                    resolvedRailPosition = selectedRoute;
                    return true;
                }

                IReadOnlyList<TrainInstanceId> ResolveOverlapTrainUnitsForRequirement1(
                    RailPosition centerRailPosition,
                    RailPositionOverlapDetector.OverlapIndex requirement1OverlapIndex,
                    int trainLength,
                    out RailPosition requirement1SnapStartPoint,
                    out TrainInstanceId requirement1TargetTrainInstanceId,
                    out bool requirement1AttachFacingForward)
                {
                    requirement1SnapStartPoint = null;
                    requirement1TargetTrainInstanceId = TrainInstanceId.Empty;
                    requirement1AttachFacingForward = true;
                    // listA(N'+M')候補と既存TrainUnit全体(listB)の多:多を先に一括判定する
                    // Run a many-to-many precheck between listA(N'+M') and all existing train units(listB)
                    var allTrainUnitRailPositionsForRequirement1 = new List<RailPosition>();

                    foreach (var pair in _trainUnitCache.Units)
                    {
                        var unit = pair.Value;
                        if (unit == null || unit.RailPosition == null)
                        {
                            continue;
                        }
                        allTrainUnitRailPositionsForRequirement1.Add(unit.RailPosition);
                    }

                    var allTrainUnitOverlapIndex = RailPositionOverlapDetector.CreateIndex(allTrainUnitRailPositionsForRequirement1);
                    if (!RailPositionOverlapDetector.HasOverlap(requirement1OverlapIndex, allTrainUnitOverlapIndex))
                    {
                        return Array.Empty<TrainInstanceId>();
                    }

                    // 一括判定ヒット後に多:1で再調査し、中心点に最も近いTrainUnitを1件だけ選ぶ
                    // After many-to-many precheck hit, rescan by many-to-one and pick only the closest train unit
                    var centerForwardPoint = centerRailPosition.GetHeadRailPosition();
                    var centerBackwardPoint = centerForwardPoint.DeepCopy();
                    centerBackwardPoint.Reverse();
                    var nearestTrainInstanceId = TrainInstanceId.Empty;
                    var nearestDistance = int.MaxValue;
                    var nearestSnapStartPoint = default(RailPosition);
                    var nearestAttachFacingForward = true;
                    var maxSnapDistance = trainLength / 2 + Requirement1AdditionalMarginLength;

                    foreach (var pair in _trainUnitCache.Units)
                    {
                        var trainInstanceId = pair.Key;
                        var unit = pair.Value;
                        if (unit == null || unit.RailPosition == null)
                        {
                            continue;
                        }
                        if (!RailPositionOverlapDetector.HasOverlap(unit.RailPosition, requirement1OverlapIndex))
                        {
                            continue;
                        }

                        // 近傍距離は center(前後) -> unit端点(先端reverse/最後尾) の4通り最短を採用する
                        // Use the minimum among 4 distances: center(forward/backward) -> unit endpoints(head-reversed/rear)
                        var distance = CalculateNearestSnapDistance(
                            centerForwardPoint,
                            centerBackwardPoint,
                            unit.RailPosition,
                            maxSnapDistance,
                            out var snapStartPoint,
                            out var attachFacingForward);
                        if (distance < 0)
                        {
                            continue;
                        }
                        if (distance >= nearestDistance)
                        {
                            continue;
                        }
                        nearestDistance = distance;
                        nearestTrainInstanceId = trainInstanceId;
                        nearestSnapStartPoint = snapStartPoint;
                        nearestAttachFacingForward = attachFacingForward;
                    }

                    if (nearestTrainInstanceId == TrainInstanceId.Empty)
                    {
                        return Array.Empty<TrainInstanceId>();
                    }
                    requirement1SnapStartPoint = nearestSnapStartPoint;
                    requirement1TargetTrainInstanceId = nearestTrainInstanceId;
                    requirement1AttachFacingForward = nearestAttachFacingForward;
                    return new[] { nearestTrainInstanceId };

                    #region Internal

                    int CalculateNearestSnapDistance(
                        RailPosition centerForward,
                        RailPosition centerBackward,
                        RailPosition unitRailPosition,
                        int maxCandidateDistance,
                        out RailPosition snapStartPoint,
                        out bool attachFacingForward)
                    {
                        snapStartPoint = null;
                        attachFacingForward = true;
                        if (centerForward == null || centerBackward == null || unitRailPosition == null)
                        {
                            return -1;
                        }

                        // 先端側はreverseして「その端点から外側方向」へ伸ばす向きで評価する
                        // For the head endpoint, reverse to evaluate outward direction from that endpoint
                        var unitHeadReversed = unitRailPosition.GetHeadRailPosition();
                        unitHeadReversed.Reverse();
                        var unitRearPoint = unitRailPosition.GetRearRailPosition();

                        var minDistance = int.MaxValue;
                        var minDistanceSnapStartPoint = default(RailPosition);
                        var minDistanceAttachFacingForward = true;
                        UpdateMinDistance(centerForward, unitHeadReversed, true);
                        UpdateMinDistance(centerForward, unitRearPoint, true);
                        UpdateMinDistance(centerBackward, unitHeadReversed, false);
                        UpdateMinDistance(centerBackward, unitRearPoint, false);
                        if (minDistance == int.MaxValue)
                        {
                            return -1;
                        }
                        snapStartPoint = minDistanceSnapStartPoint;
                        attachFacingForward = minDistanceAttachFacingForward;
                        return minDistance;

                        #region Internal

                        void UpdateMinDistance(RailPosition from, RailPosition to, bool isCenterForwardSide)
                        {
                            var distance = RailPositionRouteDistanceFinder.FindShortestDistance(from, to);
                            // 要件1の探索半径(車両半長+マージン)を超える候補は除外する
                            // Exclude candidates that are outside requirement-1 search radius (half length + margin)
                            if (distance < 0 || distance > maxCandidateDistance)
                            {
                                return;
                            }
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                minDistanceSnapStartPoint = to.DeepCopy();
                                minDistanceAttachFacingForward = isCenterForwardSide;
                            }
                        }

                        #endregion
                    }

                    #endregion
                }

                bool TryRebuildRequirement1SnapCandidates(RailPosition snapStartPoint, int trainLength, out List<RailPosition> routes, out int routeCount)
                {
                    // 要件1: 接続点からtrainLengthぶんを前方DFSで全探索する
                    // Requirement 1: enumerate all forward DFS routes with trainLength from the snap point
                    routes = new List<RailPosition>();
                    routeCount = 0;
                    if (snapStartPoint == null || trainLength <= 0)
                    {
                        return false;
                    }
                    snapStartPoint.Reverse();

                    var traceStartPoint = snapStartPoint.GetHeadRailPosition();
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

                RailPositionOverlapDetector.OverlapIndex CreateRequirement1OverlapIndex(RailPosition centerRailPosition, int trainLength)
                {
                    // 要件1専用の前後マージン探索結果を再構築する
                    // Rebuild requirement-1 specific front/rear margin probe routes
                    var requirement1OverlapProbeRoutes = new List<RailPosition>();

                    var frontLengthWithMargin = (trainLength + 1) / 2 + Requirement1AdditionalMarginLength;
                    var rearLengthWithMargin = trainLength / 2 + Requirement1AdditionalMarginLength;
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

                    // マージン探索が成立しない場合は要件1不成立として扱う
                    // If margin probing fails, treat requirement 1 as not satisfied

                    return RailPositionOverlapDetector.CreateIndex(requirement1OverlapProbeRoutes);
                }

                bool TryRebuildSelectionCandidates(RailPosition centerRailPosition, int trainLength, out List<RailPosition> frontRoutes, out List<RailPosition> rearRoutesFromCenter, out int routePairCount)
                {
                    // 要件4の候補経路を毎フレーム再構築する
                    // Rebuild requirement-4 candidate routes every frame
                    frontRoutes = new List<RailPosition>();
                    rearRoutesFromCenter = new List<RailPosition>();
                    routePairCount = 0;

                    var frontLength = (trainLength + 1) / 2;
                    var rearLength = trainLength / 2;
                    if (!TryBuildFrontRearRoutes(
                            centerRailPosition,
                            frontLength,
                            rearLength,
                            frontRoutes,
                            rearRoutesFromCenter))
                    {
                        return false;
                    }

                    var pairCount = frontRoutes.Count * rearRoutesFromCenter.Count;
                    routePairCount = pairCount > int.MaxValue ? int.MaxValue : (int)pairCount;
                    return routePairCount > 0;
                }

                bool TryBuildFrontRearRoutes(RailPosition centerRailPosition, int frontLength, int rearLength, List<RailPosition> frontRoutes, List<RailPosition> rearRoutesFromCenter)
                {
                    // 共通DFS: 中心点から前後を探索し、front/rearの向きへ正規化する
                    // Shared DFS: trace both directions from center and normalize to front/rear orientation
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

                    // 後方探索結果は反転した中心点基準なので、center->rear方向に戻す
                    // Rear traces are based on reversed center, so reverse back to center->rear direction
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

                bool TryCombineRoutes(RailPosition frontRoute, RailPosition rearRouteFromCenter, bool reverseSelected, out RailPosition combinedRoute)
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

                    var mergedRoute = frontRoute.DeepCopy();
                    mergedRoute.AppendRailPositionAtRear(rearRouteFromCenter.DeepCopy());
                    if (reverseSelected)
                    {
                        mergedRoute.Reverse();
                    }
                    combinedRoute = mergedRoute;
                    return true;
                }

                #endregion
            }

            #endregion
        }
    }
}
