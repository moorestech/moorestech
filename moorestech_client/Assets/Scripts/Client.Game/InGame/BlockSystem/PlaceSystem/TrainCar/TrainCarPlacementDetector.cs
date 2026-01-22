using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Game.Train.RailGraph;
using Game.Train.RailCalc;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Mooresmaster.Model.TrainModule;
using UnityEngine;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public readonly struct TrainCarPlacementHit
    {
        public TrainCarPlacementHit(bool isPlaceable, RailPositionSaveData railPosition)
        {
            IsPlaceable = isPlaceable;
            RailPosition = railPosition;
        }
        
        public bool IsPlaceable { get; }
        public RailPositionSaveData RailPosition { get; }
    }
    
    public interface ITrainCarPlacementDetector
    {
        bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit);
    }
    
    public class TrainCarPlacementDetector : ITrainCarPlacementDetector
    {
        private const int CurveSampleCount = 512;
        private const float MinCurveLength = 1e-4f;
        private readonly Camera _mainCamera;
        private readonly RailGraphClientCache _cache;
        private readonly RailPathTracer _pathTracer;
        
        public TrainCarPlacementDetector(Camera mainCamera, RailGraphClientCache cache)
        {
            _mainCamera = mainCamera;
            _cache = cache;
            _pathTracer = new RailPathTracer(_cache);
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

                bool TryBuildRailPosition(Vector3 hitPosition, RailObjectIdCarrier carrier, int trainLength, out RailPositionSaveData railPositionSaveData)
                {
                    railPositionSaveData = null;
                    // 入力を検証し、対象レール区間（ノード）を解決する
                    // Validate inputs and resolve the rail segment
                    if (carrier == null || trainLength <= 0)
                    {
                        return false;
                    }
                    var railObjectId = carrier.GetRailObjectId();
                    if (!TryResolveCanonicalNodes(railObjectId, out var canonicalFromId, out var canonicalToId, out var canonicalFromNode, out var canonicalToNode))
                    {
                        return false;
                    }

                    // カーブ上の最近点を求め、始点からの距離（弧長）を算出する
                    // Find the closest point on the curve and its distance
                    if (!TryFindClosestPointOnCurve(canonicalFromNode, canonicalToNode, hitPosition, out var distanceFromStartWorld, out var curveLengthWorld))
                    {
                        return false;
                    }

                    var distanceToEndWorld = curveLengthWorld - distanceFromStartWorld;

                    // 先頭側（進行方向）の区間距離と、次ノードまでのオフセットを算出する
                    // Resolve the leading segment distance and offset
                    var segmentDistance = canonicalFromNode.GetDistanceToNode(canonicalToNode);
                    if (segmentDistance <= 0)
                    {
                        return false;
                    }
                    var distanceToNext = Mathf.RoundToInt(distanceToEndWorld * BezierUtility.RAIL_LENGTH_SCALE);
                    if (distanceToNext < 0 || distanceToNext > segmentDistance)
                    {
                        return false;
                    }

                    // 中心（配置点）から両方向へ辿り、必要長さ分のスナップショットを構築する
                    // Trace both directions from the center to build the snapshot
                    if (!_pathTracer.TryTraceCentered(canonicalFromId, canonicalToId, distanceToNext, trainLength, out var traceResult))
                    {
                        return false;
                    }

                    railPositionSaveData = new RailPositionSaveData
                    {
                        TrainLength = trainLength,
                        DistanceToNextNode = traceResult.DistanceToNextNode,
                        RailSnapshot = traceResult.RailSnapshot
                    };
                    return true;
                }

                bool TryResolveCanonicalNodes(ulong railObjectId, out int canonicalFromId, out int canonicalToId, out IRailNode canonicalFromNode, out IRailNode canonicalToNode)
                {
                    canonicalFromId = (int)(railObjectId & 0xffffffff);
                    canonicalToId = (int)(railObjectId >> 32);
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

                bool TryFindClosestPointOnCurve(IRailNode startNode, IRailNode endNode, Vector3 hitPosition, out float distanceFromStart, out float curveLength)
                {
                    distanceFromStart = 0f;
                    curveLength = 0f;
                    // ベジエ制御点とサンプル距離を計算する
                    // Build Bezier control points and sample distances
                    if (startNode == null || endNode == null)
                    {
                        return false;
                    }

                    var startControl = startNode.FrontControlPoint;
                    var endControl = endNode.BackControlPoint;
                    BezierUtility.BuildRelativeControlPoints(startControl, endControl, out var origin, out var p0, out var p1, out var p2, out var p3);

                    var steps = CurveSampleCount;
                    var arcLengths = new float[steps + 1];
                    var bestIndex = 0;
                    var bestDistanceSq = (origin - hitPosition).sqrMagnitude;

                    arcLengths[0] = 0f;
                    var previous = origin + BezierUtility.GetBezierPoint(p0, p1, p2, p3, 0f);

                    for (var i = 1; i <= steps; i++)
                    {
                        // サンプル位置と累積距離を更新する
                        // Update sample position and cumulative distance
                        var t = (float)i / steps;
                        var point = origin + BezierUtility.GetBezierPoint(p0, p1, p2, p3, t);
                        arcLengths[i] = arcLengths[i - 1] + Vector3.Distance(previous, point);
                        var distanceSq = (point - hitPosition).sqrMagnitude;
                        if (distanceSq < bestDistanceSq)
                        {
                            bestDistanceSq = distanceSq;
                            bestIndex = i;
                        }
                        previous = point;
                    }

                    curveLength = arcLengths[steps];
                    if (curveLength <= MinCurveLength)
                    {
                        return false;
                    }
                    distanceFromStart = arcLengths[bestIndex];
                    return true;
                }

                #endregion
            }

            #endregion
        }
    }
}
