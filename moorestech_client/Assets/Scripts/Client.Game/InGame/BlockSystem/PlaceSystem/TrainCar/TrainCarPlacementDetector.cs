using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Game.Train.RailGraph;
using Game.Train.RailCalc;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using System;
using System.Collections.Generic;
using UnityEngine;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public readonly struct TrainCarPlacementHit
    {
        public TrainCarPlacementHit(bool isPlaceable, RailPosition railPosition)
        {
            IsPlaceable = isPlaceable;
            RailPosition = railPosition;
        }
        
        public bool IsPlaceable { get; }
        public RailPosition RailPosition { get; }
    }
    
    public interface ITrainCarPlacementDetector
    {
        bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit);
        void AdvanceSelection();
        void ResetSelection();
    }
    
    public class TrainCarPlacementDetector : ITrainCarPlacementDetector
    {
        private readonly Camera _mainCamera;
        private readonly RailGraphClientCache _cache;
        private readonly RailPathTracer _pathTracer;
        private readonly TrainCarCurveHitDistanceResolver _curveHitDistanceResolver;
        private readonly List<RailPosition> _frontRoutes = new();
        private readonly List<RailPosition> _rearRoutesFromCenter = new();
        private bool _hasCandidateKey;
        private PlacementCandidateKey _candidateKey;
        private long _selectionStep;
        private long _routePairCount;
        
        public TrainCarPlacementDetector(Camera mainCamera, RailGraphClientCache cache)
        {
            _mainCamera = mainCamera;
            _cache = cache;
            _pathTracer = new RailPathTracer(_cache);
            _curveHitDistanceResolver = new TrainCarCurveHitDistanceResolver();
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
            _hasCandidateKey = false;
            _frontRoutes.Clear();
            _rearRoutesFromCenter.Clear();
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
                var isPlaceable = TryBuildRailPosition(hitPos, railCarrier, trainLength, out var railPosition);
                result = new TrainCarPlacementHit(isPlaceable, railPosition);
                return true;

                #region Internal

                bool TryBuildRailPosition(Vector3 hitPosition, RailObjectIdCarrier carrier, int trainLength, out RailPosition railPosition)
                {
                    railPosition = null;
                    // 入力を検証し、対象レール区間（ノード）を解決する
                    // Validate inputs and resolve the rail segment
                    if (carrier == null || trainLength <= 0)
                    {
                        return false;
                    }
                    var railObjectId = carrier.GetRailObjectId();
                    if (!TryResolveCanonicalNodes(railObjectId, out var canonicalFromNode, out var canonicalToNode))
                    {
                        return false;
                    }

                    // カーブ上の最近点を求め、始点からの距離（弧長）を算出する
                    // Find the closest point on the curve and its distance
                    if (!_curveHitDistanceResolver.TryFindDistanceFromStartOnCurve(canonicalFromNode, canonicalToNode, hitPosition, out var distanceFromStartWorld))
                    {
                        return false;
                    }

                    // 先頭側（進行方向）の区間距離と、次ノードまでのオフセットを算出する
                    // Resolve the leading segment distance and offset
                    var segmentDistance = canonicalFromNode.GetDistanceToNode(canonicalToNode);
                    if (segmentDistance <= 0)
                    {
                        return false;
                    }
                    var distanceFromStartRail = Mathf.RoundToInt(distanceFromStartWorld * BezierUtility.RAIL_LENGTH_SCALE);
                    distanceFromStartRail = Mathf.Clamp(distanceFromStartRail, 0, segmentDistance);
                    var distanceToNext = segmentDistance - distanceFromStartRail;

                    // 候補キーが変化した場合のみ、前後DFS候補を再計算する
                    // Rebuild DFS candidates only when the placement key changes
                    var candidateKey = new PlacementCandidateKey(railObjectId, distanceFromStartRail, trainLength);
                    if (!_hasCandidateKey || !_candidateKey.Equals(candidateKey))
                    {
                        if (!TryRebuildCandidates(canonicalFromNode, canonicalToNode, distanceToNext, trainLength, candidateKey))
                        {
                            return false;
                        }
                    }
                    if (_routePairCount <= 0)
                    {
                        return false;
                    }

                    // 現在の選択状態(経路+反転)から最終RailPositionを構築する
                    // Build final RailPosition from current route/reverse selection
                    if (TryBuildSelectedRailPosition(out railPosition))
                    {
                        return true;
                    }

                    // キャッシュ不整合時は候補を再構築して再試行する
                    // Rebuild candidates once and retry when cached data gets inconsistent
                    if (!TryRebuildCandidates(canonicalFromNode, canonicalToNode, distanceToNext, trainLength, candidateKey))
                    {
                        return false;
                    }
                    return TryBuildSelectedRailPosition(out railPosition);
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

                    var frontRoute = _frontRoutes[frontIndex];
                    var rearRoute = _rearRoutesFromCenter[rearIndex];
                    if (!TryCombineRoutes(frontRoute, rearRoute, reverseSelected, out resolvedRailPosition))
                    {
                        return false;
                    }
                    return resolvedRailPosition != null;
                }

                bool TryRebuildCandidates(IRailNode canonicalFromNode, IRailNode canonicalToNode, int distanceToNext, int trainLength, PlacementCandidateKey candidateKey)
                {
                    var previousSelectionStep = _selectionStep;
                    var hadPreviousCandidates = _routePairCount > 0;

                    // 中心点から前輪側DFS候補を全列挙する
                    // Enumerate all front-wheel-side routes from center point
                    _frontRoutes.Clear();
                    _rearRoutesFromCenter.Clear();
                    _routePairCount = 0;

                    var centerPoint = new RailPosition(new List<IRailNode> { canonicalToNode, canonicalFromNode }, 0, distanceToNext);
                    var frontLength = (trainLength + 1) / 2;
                    if (!_pathTracer.TryTraceForwardRoutesByDfs(centerPoint, frontLength, out var frontRoutes) || frontRoutes == null || frontRoutes.Count <= 0)
                    {
                        return false;
                    }
                    _frontRoutes.AddRange(frontRoutes);

                    // 中心点を反転して後輪側DFS候補を全列挙し、中心始点向きへ戻す
                    // Reverse center point, enumerate rear-wheel routes, and convert back to center-start orientation
                    var reversedCenterPoint = centerPoint.DeepCopy();
                    reversedCenterPoint.Reverse();
                    var rearLength = trainLength / 2;
                    if (!_pathTracer.TryTraceForwardRoutesByDfs(reversedCenterPoint, rearLength, out var rearRoutesReversed) || rearRoutesReversed == null || rearRoutesReversed.Count <= 0)
                    {
                        return false;
                    }

                    for (var i = 0; i < rearRoutesReversed.Count; i++)
                    {
                        var route = rearRoutesReversed[i]?.DeepCopy();
                        if (route == null)
                        {
                            continue;
                        }
                        route.Reverse();
                        _rearRoutesFromCenter.Add(route);
                    }
                    if (_rearRoutesFromCenter.Count <= 0)
                    {
                        return false;
                    }

                    _candidateKey = candidateKey;
                    _hasCandidateKey = true;
                    _routePairCount = (long)_frontRoutes.Count * _rearRoutesFromCenter.Count;
                    // 候補再構築時もRキー選択ステップを維持する
                    // Keep the R-key selection step even after candidate rebuild
                    _selectionStep = hadPreviousCandidates ? previousSelectionStep : 0;
                    return _routePairCount > 0;
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

                bool TryResolveCanonicalNodes(ulong railObjectId, out IRailNode canonicalFromNode, out IRailNode canonicalToNode)
                {
                    var canonicalFromId = (int)(railObjectId & 0xffffffff);
                    var canonicalToId = (int)(railObjectId >> 32);
                    canonicalFromNode = null;
                    canonicalToNode = null;
                    if (!_cache.TryGetNode(canonicalFromId, out canonicalFromNode))
                    {
                        return false;
                    }
                    if (!_cache.TryGetNode(canonicalToId, out canonicalToNode))
                    {
                        return false;
                    }
                    return true;
                }

                #endregion
            }

            #endregion
        }

        #region Internal

        private readonly struct PlacementCandidateKey : IEquatable<PlacementCandidateKey>
        {
            public readonly ulong RailObjectId;
            public readonly int DistanceFromStartRail;
            public readonly int TrainLength;

            public PlacementCandidateKey(ulong railObjectId, int distanceFromStartRail, int trainLength)
            {
                RailObjectId = railObjectId;
                DistanceFromStartRail = distanceFromStartRail;
                TrainLength = trainLength;
            }

            public bool Equals(PlacementCandidateKey other)
            {
                return RailObjectId == other.RailObjectId && DistanceFromStartRail == other.DistanceFromStartRail && TrainLength == other.TrainLength;
            }
        }

        #endregion
    }
}
