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
/// 要件1 連結モード
/// レイキャストのrailposition=centerRailPositionから前方後方にDFSをする。距離はtrainCar.Length/2にマージンを足した距離(ちょっと先でもスナップするように)
/// 前方N`個、後方M`個のrailposition候補ができる。これら(N`+M`)と既存のtrainunit全体のRailPositionの重複を検査
/// 1つでも重複していたらどのTrainUnitか再調査、してなければ要件2へ
/// 重複している中で一番近いTrainUnitを1つ抽出。その点にスナップする。
/// スナップはDFSでもう片方の端点を全探索する
/// 見つかった経路をSとする
/// なければ要件2へ
/// これらSと既存のtrainunit全体のRailPositionの重複を検査(1:多)、Sからtrainunitと重複がある経路を除去してS`
/// なければ要件2へ
/// S` -> _selectionStepで1つを選択
/// 
/// 要件2 新規モード
/// centerRailPositionから前方後方にtrainCar.Length/2の距離 DFSする
/// その全経路の中に駅nodeがある場合、一番centerRailPositionに近いnodeに自動スナップ
/// なければ要件3へ
/// スナップはDFS(centerRailPosition方向にのみ)でもう片方の端点を全探索する
/// なければ要件3へ
/// 見つかった経路をTとする
/// これらTと既存のtrainunit全体のRailPositionの重複を検査(1:多)、Tからtrainunitと重複がある経路を除去してT`
/// なければ要件3へ
/// T` -> _selectionStepで1つを選択
///
/// 要件3 新規モード
/// centerRailPositionから前方後方にtrainCar.Length/2の距離 DFSする
/// そのDFSで経路長がtrainCar.Length/2より短くて検出できなかった経路のみをピックアップ
/// なければ要件4へ
/// 一番centerRailPositionに近い距離でreturnしてきた経路の先端部分(レールの端)にスナップ
/// DFS(centerRailPosition方向にのみ)でもう片方の端点を全探索する
/// なければ要件4へ
/// 見つかった経路をUとする
/// これらUと既存のtrainunit全体のRailPositionの重複を検査(1:多)、Uからtrainunitと重複がある経路を除去してU`
/// なければ要件4へ
/// U` -> _selectionStepで1つを選択
/// 
/// 要件4 新規モード
/// centerRailPositionから前方後方にtrainCar.Length/2の距離 DFSする
/// 見つかった経路をVとする
/// なければ配置不可
/// V -> _selectionStepで1つを選択した経路をv
/// vと既存のtrainunit全体のRailPositionの重複を検査(1:多)
/// 重複していたら配置不可、なければ配置可能


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
        private readonly TrainCarCenterRailPositionResolver _centerRailPositionResolver;
        private readonly TrainCarPlacementRouteService _routeService;
        private readonly TrainCarPlacementSelectionResolver _selectionResolver;
        private readonly TrainCarPlacementRequirement2Resolver _requirement2Resolver;
        private readonly TrainCarPlacementRequirement3Resolver _requirement3Resolver;
        private int _routePairCount;
        private int _selectionStep;
        
        public TrainCarPlacementDetector(Camera mainCamera, RailGraphClientCache cache, TrainUnitClientCache trainUnitCache)
        {
            _mainCamera = mainCamera;
            _trainUnitCache = trainUnitCache;
            var pathTracer = new RailPathTracer(cache);
            var snapPointFinder = new TrainCarPlacementSnapPointFinder();
            _routeService = new TrainCarPlacementRouteService(pathTracer);
            _selectionResolver = new TrainCarPlacementSelectionResolver();
            _requirement2Resolver = new TrainCarPlacementRequirement2Resolver(_routeService, snapPointFinder);
            _requirement3Resolver = new TrainCarPlacementRequirement3Resolver(_routeService, snapPointFinder);
            _centerRailPositionResolver = new TrainCarCenterRailPositionResolver(cache, new TrainCarCurveHitDistanceResolver());
        }

        public void AdvanceSelection()
        {
            // 候補総数に応じて次の状態へ進める
            // Advance to the next state within current candidate count
            var totalStateCount = _routePairCount * 2;
            if (totalStateCount == 0)
                return;
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
                var railPosition = default(RailPosition);
                var overlapTrainInstanceIds = default(IReadOnlyList<TrainInstanceId>);
                var placementMode = TrainCarPlacementMode.CreateNewTrainUnit;
                var targetTrainInstanceId = TrainInstanceId.Empty;
                var attachCarFacingForward = true;
                var attachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
                var isPlaceable = TryBuildRailPosition(
                    hitPos,
                    railCarrier,
                    trainLength,
                    out railPosition,
                    out overlapTrainInstanceIds,
                    out placementMode,
                    out targetTrainInstanceId,
                    out attachCarFacingForward,
                    out attachTargetEndpoint);
                result = new TrainCarPlacementHit(
                    isPlaceable,
                    railPosition,
                    overlapTrainInstanceIds,
                    placementMode,
                    targetTrainInstanceId,
                    attachCarFacingForward,
                    attachTargetEndpoint);
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
                    out bool attachCarFacingForward,
                    out TrainCarAttachTargetEndpoint attachTargetEndpoint)
                {
                    railPosition = null;
                    overlapTrainInstanceIds = Array.Empty<TrainInstanceId>();
                    placementMode = TrainCarPlacementMode.CreateNewTrainUnit;
                    targetTrainInstanceId = TrainInstanceId.Empty;
                    attachCarFacingForward = true;
                    attachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
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
                    // Rキー反転をここで適応
                    // Apply R-key reversal at this stage to keep the same center point and flip the candidates instead
                    if (_selectionStep % 2 == 1)
                    {
                        centerRailPosition.Reverse();
                    }
                    

                    // 要件1-4共通: 既存TrainUnit全体の重複indexを1回だけ構築して使い回す
                    // Requirement 1-4 shared: build all-train overlap index once and reuse it
                    var allTrainUnitRailPositions = CreateAllTrainUnitRailPositions();
                    var allTrainUnitOverlapIndex = RailPositionOverlapDetector.CreateIndex(allTrainUnitRailPositions);

                    // 要件1: N'+M'候補と既存TrainUnit全体の重複を抽出する
                    // Requirement 1: detect overlaps between N'+M' candidates and existing train units
                    var requirement1OverlapIndex = _routeService.CreateRequirement1OverlapIndex(centerRailPosition, trainLength, Requirement1AdditionalMarginLength);
                    RailPosition requirement1SnapStartPoint;
                    TrainInstanceId requirement1TargetTrainInstanceId;
                    bool requirement1AttachFacingForward;
                    TrainCarAttachTargetEndpoint requirement1AttachTargetEndpoint;
                    overlapTrainInstanceIds = ResolveOverlapTrainUnitsForRequirement1(
                        centerRailPosition,
                        requirement1OverlapIndex,
                        allTrainUnitOverlapIndex,
                        trainLength,
                        out requirement1SnapStartPoint,
                        out requirement1TargetTrainInstanceId,
                        out requirement1AttachFacingForward,
                        out requirement1AttachTargetEndpoint);

                    // 要件1: 最短TrainUnitの接続点からS候補を作り、重複経路を除外したS'を選択する
                    // Requirement 1: build S routes from nearest unit endpoint and pick from overlap-filtered S'
                    if (requirement1SnapStartPoint != null && requirement1TargetTrainInstanceId != TrainInstanceId.Empty)
                    {
                        _routePairCount = 0;
                        if (_routeService.TryRebuildRequirement1SnapCandidates(requirement1SnapStartPoint, trainLength, out var requirement1Routes, out _))
                        {
                            var requirement1FilteredRoutes = _routeService.FilterRoutesWithoutOverlap(requirement1Routes, allTrainUnitOverlapIndex);
                            var requirement1FilteredRouteCount = requirement1FilteredRoutes.Count;
                            if (requirement1FilteredRouteCount > 0)
                            {
                                _routePairCount = requirement1FilteredRouteCount;
                                if (_selectionResolver.TryBuildRequirement1SelectedSingleRoute(
                                        requirement1FilteredRoutes,
                                        requirement1FilteredRouteCount,
                                        _selectionStep,
                                        requirement1AttachFacingForward,
                                        out railPosition))
                                {
                                    placementMode = TrainCarPlacementMode.AttachToExistingTrainUnit;
                                    targetTrainInstanceId = requirement1TargetTrainInstanceId;
                                    // 最短候補で使ったcenter前後向きを基準にし、R反転時は向きも反転させる
                                    // Base facing uses nearest center direction and flips when R-reverse is selected
                                    attachCarFacingForward = requirement1AttachFacingForward;
                                    attachTargetEndpoint = requirement1AttachTargetEndpoint;
                                    return true;
                                }
                            }
                        }
                    }
                    overlapTrainInstanceIds = Array.Empty<TrainInstanceId>();

                    // 要件2: 駅nodeスナップ候補を作り、重複除外後のT'から選択する
                    // Requirement 2: build station-snap candidates and pick from overlap-filtered T'
                    _routePairCount = 0;
                    if (_requirement2Resolver.TryResolve(
                            centerRailPosition,
                            trainLength,
                            allTrainUnitOverlapIndex,
                            out var requirement2Routes,
                            out var requirement2SnapFromCenterForward))
                    {
                        var requirement2RouteCount = requirement2Routes.Count;
                        if (requirement2RouteCount > 0)
                        {
                            _routePairCount = requirement2RouteCount;
                            if (_selectionResolver.TryBuildCreateModeSelectedSingleRoute(
                                    requirement2Routes,
                                    requirement2RouteCount,
                                    _selectionStep,
                                    requirement2SnapFromCenterForward,
                                    out railPosition))
                            {
                                return true;
                            }
                        }
                    }

                    // 要件3: レール端スナップ候補を作り、重複除外後のU'から選択する
                    // Requirement 3: build rail-end snap candidates and pick from overlap-filtered U'
                    _routePairCount = 0;
                    if (_requirement3Resolver.TryResolve(
                            centerRailPosition,
                            trainLength,
                            allTrainUnitOverlapIndex,
                            out var requirement3Routes,
                            out var requirement3SnapFromCenterForward))
                    {
                        var requirement3RouteCount = requirement3Routes.Count;
                        if (requirement3RouteCount > 0)
                        {
                            _routePairCount = requirement3RouteCount;
                            if (_selectionResolver.TryBuildCreateModeSelectedSingleRoute(
                                    requirement3Routes,
                                    requirement3RouteCount,
                                    _selectionStep,
                                    requirement3SnapFromCenterForward,
                                    out railPosition))
                            {
                                return true;
                            }
                        }
                    }

                    // 要件4候補（前輪側/後輪側）を毎フレーム再構築する
                    // Rebuild requirement-4 candidates (front/rear) every frame
                    _routePairCount = 0;
                    if (!_routeService.TryRebuildRequirement4SelectionCandidates(centerRailPosition, trainLength, out var frontRoutes, out var rearRoutesFromCenter, out var routePairCount))
                    {
                        return false;
                    }
                    _routePairCount = routePairCount;

                    // 要件4: Vから選択したvと既存TrainUnit全体を重複検査する
                    // Requirement 4: test overlap between selected v from V and all existing train units
                    if (!_routeService.TryBuildRequirement4SelectedRailPosition(frontRoutes, rearRoutesFromCenter, routePairCount, _selectionStep, out railPosition))
                    {
                        return false;
                    }
                    if (railPosition == null)
                    {
                        return false;
                    }
                    if (RailPositionOverlapDetector.HasOverlap(railPosition, allTrainUnitOverlapIndex))
                    {
                        return false;
                    }
                    return true;
                }

                // 要件1-4共通: 既存TrainUnitのRailPosition一覧を生成する
                // Requirement 1-4 shared: build the list of all existing train-unit RailPositions
                List<RailPosition> CreateAllTrainUnitRailPositions()
                {
                    var positions = new List<RailPosition>();
                    foreach (var pair in _trainUnitCache.Units)
                    {
                        var unit = pair.Value;
                        if (unit == null || unit.RailPosition == null)
                        {
                            continue;
                        }
                        positions.Add(unit.RailPosition);
                    }
                    return positions;
                }

                IReadOnlyList<TrainInstanceId> ResolveOverlapTrainUnitsForRequirement1(
                    RailPosition centerRailPosition,
                    RailPositionOverlapDetector.OverlapIndex requirement1OverlapIndex,
                    RailPositionOverlapDetector.OverlapIndex allTrainUnitOverlapIndex,
                    int trainLength,
                    out RailPosition requirement1SnapStartPoint,
                    out TrainInstanceId requirement1TargetTrainInstanceId,
                    out bool requirement1AttachFacingForward,
                    out TrainCarAttachTargetEndpoint requirement1AttachTargetEndpoint)
                {
                    requirement1SnapStartPoint = null;
                    requirement1TargetTrainInstanceId = TrainInstanceId.Empty;
                    requirement1AttachFacingForward = true;
                    requirement1AttachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
                    // listA(N'+M')候補と既存TrainUnit全体(listB)の多:多を先に一括判定する
                    // Run a many-to-many precheck between listA(N'+M') and all existing train units(listB)
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
                    var nearestAttachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
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
                            out var attachFacingForward,
                            out var attachTargetEndpoint);
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
                        nearestAttachTargetEndpoint = attachTargetEndpoint;
                    }

                    if (nearestTrainInstanceId == TrainInstanceId.Empty)
                    {
                        return Array.Empty<TrainInstanceId>();
                    }
                    requirement1SnapStartPoint = nearestSnapStartPoint;
                    requirement1TargetTrainInstanceId = nearestTrainInstanceId;
                    requirement1AttachFacingForward = nearestAttachFacingForward;
                    requirement1AttachTargetEndpoint = nearestAttachTargetEndpoint;
                    return new[] { nearestTrainInstanceId };

                    #region Internal

                    int CalculateNearestSnapDistance(
                        RailPosition centerForward,
                        RailPosition centerBackward,
                        RailPosition unitRailPosition,
                        int maxCandidateDistance,
                        out RailPosition snapStartPoint,
                        out bool attachFacingForward,
                        out TrainCarAttachTargetEndpoint attachTargetEndpoint)
                    {
                        snapStartPoint = null;
                        attachFacingForward = true;
                        attachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
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
                        var minDistanceAttachTargetEndpoint = TrainCarAttachTargetEndpoint.Head;
                        UpdateMinDistance(centerForward, unitHeadReversed, true, TrainCarAttachTargetEndpoint.Head);
                        UpdateMinDistance(centerForward, unitRearPoint, true, TrainCarAttachTargetEndpoint.Rear);
                        UpdateMinDistance(centerBackward, unitHeadReversed, false, TrainCarAttachTargetEndpoint.Head);
                        UpdateMinDistance(centerBackward, unitRearPoint, false, TrainCarAttachTargetEndpoint.Rear);
                        if (minDistance == int.MaxValue)
                        {
                            return -1;
                        }
                        snapStartPoint = minDistanceSnapStartPoint;
                        attachFacingForward = minDistanceAttachFacingForward;
                        attachTargetEndpoint = minDistanceAttachTargetEndpoint;
                        return minDistance;

                        #region Internal

                        void UpdateMinDistance(
                            RailPosition from,
                            RailPosition to,
                            bool isCenterForwardSide,
                            TrainCarAttachTargetEndpoint candidateAttachTargetEndpoint)
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
                                minDistanceAttachTargetEndpoint = candidateAttachTargetEndpoint;
                            }
                        }

                        #endregion
                    }

                    #endregion
                }

                #endregion
            }

            #endregion
        }
    }
}
