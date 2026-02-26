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
        private const int AttachSnapAdditionalMarginLength = 256;
        private readonly Camera _mainCamera;
        private readonly TrainUnitClientCache _trainUnitCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly RailPathTracer _pathTracer;
        private int _selectionStep;
        
        public TrainCarPlacementDetector(Camera mainCamera, RailGraphClientCache cache, TrainUnitClientCache trainUnitCache)
        {
            _mainCamera = mainCamera;
            _trainUnitCache = trainUnitCache;
            _railGraphCache = cache;
            _pathTracer = new RailPathTracer(cache);
        }

        public void AdvanceSelection()
        {
            // 候補総数に応じて次の状態へ進める
            // Advance to the next state within current candidate count
            _selectionStep++;
        }

        public void ResetSelection()
        {
            // 候補と選択状態を初期化する
            // Reset candidates and selection state
            _selectionStep = 0;
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
            
            // レイキャストからrailpositionを解決する
            // Resolve the rail position from the raycast hit
            if (!TrainCarCenterRailPositionResolver.TryResolveCenterRailPosition(hitPosition, railCarrier, _railGraphCache, out var hitRailPosition))
            {
                return false;
            }

            // 位置からレールスナップショットを組み立てる
            // Build the rail snapshot from the hit position
            BuildPlacement(hitRailPosition, trainCarMaster, out hit);
            return true;

            #region Internal

            bool TryResolveTrainCarMaster(ItemId itemId, out TrainCarMasterElement trainCarMasterElement)
            {
                // 手持ちアイテムが車両マスターに対応するか判定する
                // Ensure the held item represents a train car master
                return MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(itemId, out trainCarMasterElement);
            }

            void BuildPlacement(RailPosition centerRailPosition, TrainCarMasterElement trainCarMasterElement, out TrainCarPlacementHit result)
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
                    centerRailPosition, 
                    trainLength, 
                    out railPosition, 
                    out overlapTrainInstanceIds, 
                    out placementMode, 
                    out targetTrainInstanceId, 
                    out attachCarFacingForward, 
                    out attachTargetEndpoint
                    );
                result = new TrainCarPlacementHit(
                    isPlaceable, 
                    railPosition, 
                    overlapTrainInstanceIds, 
                    placementMode, 
                    targetTrainInstanceId, 
                    attachCarFacingForward, 
                    attachTargetEndpoint
                    );
                return;

                #region Internal

                bool TryBuildRailPosition(
                    RailPosition centerRailPosition,
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
                    if (trainLength < 0)
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
                    var attachProbeOverlapIndex = TrainCarPlacementRouteService.CreateAttachProbeOverlapIndex(centerRailPosition, trainLength + AttachSnapAdditionalMarginLength, _pathTracer);
                    RailPosition attachSnapStartPoint;
                    TrainInstanceId attachTargetTrainInstanceId;
                    bool attachSnapFacingForward;
                    TrainCarAttachTargetEndpoint attachSnapTargetEndpoint;
                    overlapTrainInstanceIds = TrainCarPlacementAttachSnapResolver.ResolveOverlapTrainUnitsForAttachSnap(
                        _trainUnitCache,
                        centerRailPosition,
                        attachProbeOverlapIndex,
                        allTrainUnitOverlapIndex,
                        trainLength,
                        AttachSnapAdditionalMarginLength,
                        out attachSnapStartPoint,
                        out attachTargetTrainInstanceId,
                        out attachSnapFacingForward,
                        out attachSnapTargetEndpoint);

                    // 要件1: 最短TrainUnitの接続点からS候補を作り、重複経路を除外したS'を選択する
                    // Requirement 1: build S routes from nearest unit endpoint and pick from overlap-filtered S'
                    if (attachSnapStartPoint != null && attachTargetTrainInstanceId != TrainInstanceId.Empty)
                    {
                        attachSnapStartPoint.Reverse();
                        if (_pathTracer.TryTraceForwardRoutesByDfs(attachSnapStartPoint, trainLength, out var attachSnapRoutes))
                        {
                            var attachSnapFilteredRoutes = TrainCarPlacementRouteService.FilterRoutesWithoutOverlap(attachSnapRoutes, allTrainUnitOverlapIndex);
                            if (attachSnapFilteredRoutes.Count > 0)
                            {
                                if (TrainCarPlacementSelectionResolver.TrySelectSingleRoute(
                                        attachSnapFilteredRoutes,
                                        _selectionStep,
                                        attachSnapFacingForward,
                                        out railPosition))
                                {
                                    placementMode = TrainCarPlacementMode.AttachToExistingTrainUnit;
                                    targetTrainInstanceId = attachTargetTrainInstanceId;
                                    // 最短候補で使ったcenter前後向きを基準にし、R反転時は向きも反転させる
                                    // Base facing uses nearest center direction and flips when R-reverse is selected
                                    attachCarFacingForward = attachSnapFacingForward;
                                    attachTargetEndpoint = attachSnapTargetEndpoint;
                                    return true;
                                }
                            }
                        }
                    }
                    overlapTrainInstanceIds = Array.Empty<TrainInstanceId>();

                    // 要件2: 駅nodeスナップ候補を作り、重複除外後のT'から選択する
                    // Requirement 2: build station-snap candidates and pick from overlap-filtered T'
                    if (TrainCarPlacementStationSnapResolver.TryResolveStationSnapRoutes(
                            centerRailPosition,
                            trainLength,
                            _pathTracer,
                            allTrainUnitOverlapIndex,
                            out var stationSnapRoutes,
                            out var stationSnapFromCenterForward))
                    {
                        if (stationSnapRoutes.Count > 0)
                        {
                            if (TrainCarPlacementSelectionResolver.TrySelectSingleRoute(
                                    stationSnapRoutes,
                                    _selectionStep,
                                    stationSnapFromCenterForward,
                                    out railPosition))
                            {
                                return true;
                            }
                        }
                    }

                    // 要件3: レール端スナップ候補を作り、重複除外後のU'から選択する
                    // Requirement 3: build rail-end snap candidates and pick from overlap-filtered U'
                    if (TrainCarPlacementRailEndSnapResolver.TryResolveRailEndSnapRoutes(
                            centerRailPosition,
                            trainLength,
                            _pathTracer,
                            allTrainUnitOverlapIndex,
                            out var railEndSnapRoutes,
                            out var railEndSnapFromCenterForward))
                    {
                        if (railEndSnapRoutes.Count > 0)
                        {
                            if (TrainCarPlacementSelectionResolver.TrySelectSingleRoute(
                                    railEndSnapRoutes,
                                    _selectionStep,
                                    railEndSnapFromCenterForward,
                                    out railPosition))
                            {
                                return true;
                            }
                        }
                    }

                    // 要件4候補（前輪側/後輪側）を毎フレーム再構築する
                    // Rebuild requirement-4 candidates (front/rear) every frame
                    if (!TrainCarPlacementRouteService.TryBuildCarPlacementSelectionCandidates(centerRailPosition, trainLength, _pathTracer, out var frontRoutes, out var rearRoutesFromCenter, out var routePairCount))
                    {
                        return false;
                    }

                    // 要件4: Vから選択したvと既存TrainUnit全体を重複検査する
                    // Requirement 4: test overlap between selected v from V and all existing train units
                    if (!TrainCarPlacementRouteService.TryBuildSelectedCarPlacement(frontRoutes, rearRoutesFromCenter, routePairCount, _selectionStep, out railPosition))
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


                #endregion
            }

            #endregion
        }
    }
}
