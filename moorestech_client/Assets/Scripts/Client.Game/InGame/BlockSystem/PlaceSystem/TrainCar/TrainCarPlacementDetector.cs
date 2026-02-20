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
        private readonly List<RailPosition> _frontRoutes = new();
        private readonly List<RailPosition> _rearRoutesFromCenter = new();
        private readonly List<TrainInstanceId> _overlapTrainIdsForRequirement1 = new();
        private readonly List<RailPosition> _allTrainUnitRailPositionsForRequirement1 = new();
        private readonly List<RailPosition> _requirement1OverlapProbeRoutes = new();
        private RailPositionOverlapDetector.OverlapIndex _requirement1OverlapIndex = RailPositionOverlapDetector.CreateIndex(Array.Empty<RailPosition>());
        private long _routePairCount;
        private long _selectionStep;
        
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
            _frontRoutes.Clear();
            _rearRoutesFromCenter.Clear();
            _overlapTrainIdsForRequirement1.Clear();
            _allTrainUnitRailPositionsForRequirement1.Clear();
            _requirement1OverlapProbeRoutes.Clear();
            _requirement1OverlapIndex = RailPositionOverlapDetector.CreateIndex(Array.Empty<RailPosition>());
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
                var isPlaceable = TryBuildRailPosition(hitPos, railCarrier, trainLength, out var railPosition, out var overlapTrainInstanceIds);
                result = new TrainCarPlacementHit(isPlaceable, railPosition, overlapTrainInstanceIds);
                return true;

                #region Internal

                bool TryBuildRailPosition(Vector3 hitPosition, RailObjectIdCarrier carrier, int trainLength, out RailPosition railPosition, out IReadOnlyList<TrainInstanceId> overlapTrainInstanceIds)
                {
                    railPosition = null;
                    overlapTrainInstanceIds = Array.Empty<TrainInstanceId>();
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

                    // 要件4候補（前輪側/後輪側）を毎フレーム再構築する
                    // Rebuild requirement-4 candidates (front/rear) every frame
                    if (!TryRebuildSelectionCandidates(centerRailPosition, trainLength))
                    {
                        return false;
                    }
                    RebuildRequirement1OverlapIndex(centerRailPosition, trainLength);

                    // 要件1: N'+M'候補と既存TrainUnit全体の重複を抽出する
                    // Requirement 1: detect overlaps between N'+M' candidates and existing train units
                    overlapTrainInstanceIds = ResolveOverlapTrainUnitsForRequirement1();

                    // 要件1: 重複先TrainUnitへの自動スナップは次PRで実装する
                    // Requirement 1: auto-snap to overlapped train units will be implemented in a follow-up PR
                    if (overlapTrainInstanceIds.Count > 0)
                    {
                        // TODO: 要件1の先頭/最後尾スナップ選択ロジックをここで実装する
                        // TODO: Implement requirement-1 snap selection (head/tail) here
                    }

                    // 要件2: 未実装
                    // Requirement 2: not implemented yet
                    // TODO: 要件2の配置判定ロジックを実装する
                    // TODO: Implement requirement-2 placement logic

                    // 要件3: 未実装
                    // Requirement 3: not implemented yet
                    // TODO: 要件3の配置判定ロジックを実装する
                    // TODO: Implement requirement-3 placement logic

                    // 要件4: 現在のインデックス選択経路を利用する
                    // Requirement 4: use the currently indexed route
                    // 現在の選択状態(経路+反転)から最終RailPositionを構築する
                    // Build final RailPosition from current route/reverse selection
                    if (TryBuildSelectedRailPosition(out railPosition))
                    {
                        return true;
                    }

                    return false;
                }

                bool TryBuildSelectedRailPosition(out RailPosition resolvedRailPosition)
                {
                    resolvedRailPosition = null;
                    if (_routePairCount <= 0 || _frontRoutes.Count <= 0 || _rearRoutesFromCenter.Count <= 0)
                    {
                        return false;
                    }

                    var totalStateCount = _routePairCount * 2;
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
                    var rearCount = _rearRoutesFromCenter.Count;
                    var frontIndex = (int)(routePairIndex / rearCount);
                    var rearIndex = (int)(routePairIndex % rearCount);
                    if (frontIndex < 0 || frontIndex >= _frontRoutes.Count || rearIndex < 0 || rearIndex >= _rearRoutesFromCenter.Count)
                    {
                        return false;
                    }

                    return TryCombineRoutes(_frontRoutes[frontIndex], _rearRoutesFromCenter[rearIndex], reverseSelected, out resolvedRailPosition);
                }

                IReadOnlyList<TrainInstanceId> ResolveOverlapTrainUnitsForRequirement1()
                {
                    // listA(N'+M')候補と既存TrainUnit全体(listB)の多:多を先に一括判定する
                    // Run a many-to-many precheck between listA(N'+M') and all existing train units(listB)
                    _overlapTrainIdsForRequirement1.Clear();
                    _allTrainUnitRailPositionsForRequirement1.Clear();

                    foreach (var pair in _trainUnitCache.Units)
                    {
                        var unit = pair.Value;
                        if (unit == null || unit.RailPosition == null)
                        {
                            continue;
                        }
                        _allTrainUnitRailPositionsForRequirement1.Add(unit.RailPosition);
                    }

                    var allTrainUnitOverlapIndex = RailPositionOverlapDetector.CreateIndex(_allTrainUnitRailPositionsForRequirement1);
                    if (!RailPositionOverlapDetector.HasOverlap(_requirement1OverlapIndex, allTrainUnitOverlapIndex))
                    {
                        return _overlapTrainIdsForRequirement1;
                    }

                    // 一括判定でヒットした場合のみ、どのTrainUnitかを個別に再調査する
                    // Only when precheck hits, rescan per TrainUnit to identify exact overlapped ids
                    foreach (var pair in _trainUnitCache.Units)
                    {
                        var trainInstanceId = pair.Key;
                        var unit = pair.Value;
                        if (unit == null || unit.RailPosition == null)
                        {
                            continue;
                        }
                        if (!RailPositionOverlapDetector.HasOverlap(unit.RailPosition, _requirement1OverlapIndex))
                        {
                            continue;
                        }
                        _overlapTrainIdsForRequirement1.Add(trainInstanceId);
                    }
                    return _overlapTrainIdsForRequirement1;
                }

                void RebuildRequirement1OverlapIndex(RailPosition centerRailPosition, int trainLength)
                {
                    // 要件1専用の前後マージン探索結果を再構築する
                    // Rebuild requirement-1 specific front/rear margin probe routes
                    _requirement1OverlapProbeRoutes.Clear();

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
                        _requirement1OverlapProbeRoutes.AddRange(requirement1FrontRoutes);
                        _requirement1OverlapProbeRoutes.AddRange(requirement1RearRoutes);
                    }

                    // マージン探索が成立しない場合は通常候補へフォールバックする
                    // Fallback to regular candidates when margin probing fails
                    if (!hasMarginRoute)
                    {
                        _requirement1OverlapProbeRoutes.AddRange(_frontRoutes);
                        _requirement1OverlapProbeRoutes.AddRange(_rearRoutesFromCenter);
                    }

                    _requirement1OverlapIndex = RailPositionOverlapDetector.CreateIndex(_requirement1OverlapProbeRoutes);
                }

                bool TryRebuildSelectionCandidates(RailPosition centerRailPosition, int trainLength)
                {
                    // 要件4の候補経路を毎フレーム再構築する
                    // Rebuild requirement-4 candidate routes every frame
                    _frontRoutes.Clear();
                    _rearRoutesFromCenter.Clear();
                    _routePairCount = 0;

                    var frontLength = (trainLength + 1) / 2;
                    var rearLength = trainLength / 2;
                    if (!TryBuildFrontRearRoutes(
                            centerRailPosition,
                            frontLength,
                            rearLength,
                            _frontRoutes,
                            _rearRoutesFromCenter))
                    {
                        return false;
                    }

                    _routePairCount = (long)_frontRoutes.Count * _rearRoutesFromCenter.Count;
                    return _routePairCount > 0;
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
                    return combinedRoute != null;
                }

                #endregion
            }

            #endregion
        }
    }
}
