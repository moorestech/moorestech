using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train;
using Core.Master;
using Game.Train.RailGraph;
using Game.Train.Train;
using Game.Train.Utility;
using Mooresmaster.Model.TrainModule;
using System.Collections.Generic;
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
        
        public TrainCarPlacementDetector(Camera mainCamera, RailGraphClientCache cache)
        {
            _mainCamera = mainCamera;
            _cache = cache;
        }

        public bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit)
        {
            hit = default;
            // 列車マスターを解決する
            // Resolve the train master definition
            if (!TryResolveTrainMaster(holdingItemId, out var trainCarMaster))
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

            bool TryResolveTrainMaster(ItemId itemId, out TrainCarMasterElement trainCarMasterElement)
            {
                // 手持ちアイテムが列車ユニットか判定する
                // Ensure the held item represents a train unit
                return MasterHolder.TrainUnitMaster.TryGetTrainUnit(itemId, out trainCarMasterElement);
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
                    // 入力を検証してレール区間を解決する
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

                    // カーブ上の最近点と距離を算出する
                    // Find the closest point on the curve and its distance
                    if (!TryFindClosestPointOnCurve(canonicalFromNode, canonicalToNode, hitPosition, out var distanceFromStartWorld, out var curveLengthWorld))
                    {
                        return false;
                    }

                    var distanceToStartWorld = distanceFromStartWorld;
                    var distanceToEndWorld = curveLengthWorld - distanceFromStartWorld;
                    var useForwardEdge = distanceToEndWorld <= distanceToStartWorld;
                    var headNodeId = useForwardEdge ? canonicalToId : (canonicalFromId ^ 1);
                    var behindNodeId = useForwardEdge ? canonicalFromId : (canonicalToId ^ 1);
                    var distanceToNextWorld = useForwardEdge ? distanceToEndWorld : distanceToStartWorld;

                    if (!_cache.TryGetNode(headNodeId, out var headNode))
                    {
                        return false;
                    }
                    if (!_cache.TryGetNode(behindNodeId, out var behindNode))
                    {
                        return false;
                    }

                    // 先頭区間の距離と進行距離を算出する
                    // Resolve the leading segment distance and offset
                    var segmentDistance = behindNode.GetDistanceToNode(headNode);
                    if (segmentDistance <= 0)
                    {
                        return false;
                    }
                    var distanceToNext = Mathf.RoundToInt(distanceToNextWorld * BezierUtility.RAIL_LENGTH_SCALE);
                    if (distanceToNext < 0 || distanceToNext > segmentDistance)
                    {
                        return false;
                    }

                    // 先頭ノードから後方へスナップショットを構築する
                    // Build the snapshot from the head node toward the rear
                    var railSnapshot = new List<ConnectionDestination> { headNode.ConnectionDestination, behindNode.ConnectionDestination };
                    var remaining = trainLength + distanceToNext - segmentDistance;
                    var currentNodeId = behindNodeId;
                    var guard = 0;
                    while (remaining > 0)
                    {
                        // 入力辺を辿って後方ノードを追加する
                        // Walk incoming edge to append rearward nodes
                        if (!TryGetIncomingEdge(currentNodeId, out var previousNodeId, out var distance))
                        {
                            return false;
                        }
                        if (distance <= 0)
                        {
                            return false;
                        }
                        if (!_cache.TryGetNode(previousNodeId, out var previousNode))
                        {
                            return false;
                        }
                        railSnapshot.Add(previousNode.ConnectionDestination);
                        remaining -= distance;
                        currentNodeId = previousNodeId;
                        guard++;
                        if (guard > _cache.ConnectNodes.Count + 1)
                        {
                            return false;
                        }
                    }

                    railPositionSaveData = new RailPositionSaveData
                    {
                        TrainLength = trainLength,
                        DistanceToNextNode = distanceToNext,
                        RailSnapshot = railSnapshot
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

                bool TryGetIncomingEdge(int nodeId, out int sourceNodeId, out int distance)
                {
                    sourceNodeId = -1;
                    distance = 0;
                    // 分岐時は最小NodeIdの入力辺を選択する
                    // Choose the incoming edge with the smallest node id
                    var found = false;
                    for (var i = 0; i < _cache.ConnectNodes.Count; i++)
                    {
                        var edges = _cache.ConnectNodes[i];
                        for (var j = 0; j < edges.Count; j++)
                        {
                            var edge = edges[j];
                            if (edge.targetId != nodeId)
                            {
                                continue;
                            }
                            if (!found || i < sourceNodeId)
                            {
                                sourceNodeId = i;
                                distance = edge.distance;
                                found = true;
                            }
                        }
                    }
                    return found;
                }

                #endregion
            }

            #endregion
        }
    }
}
