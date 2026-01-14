using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Train;
using Core.Master;
using Game.Train.RailGraph;
using Game.Train.Train;
using Game.Train.Utility;
using Mooresmaster.Model.TrainModule;
using System.Collections.Generic;
using UnityEngine;
using static Server.Protocol.PacketResponse.RailConnectionEditProtocol;
using static Client.Common.LayerConst;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar
{
    public readonly struct TrainCarPlacementHit
    {
        public TrainCarPlacementHit(RailComponentSpecifier specifier, Vector3 previewPosition, Quaternion previewRotation, bool isPlaceable, RailPositionSaveData railPosition)
        {
            Specifier = specifier;
            PreviewPosition = previewPosition;
            PreviewRotation = previewRotation;
            IsPlaceable = isPlaceable;
            RailPosition = railPosition;
        }
        
        public RailComponentSpecifier Specifier { get; }
        public Vector3 PreviewPosition { get; }
        public Quaternion PreviewRotation { get; }
        public bool IsPlaceable { get; }
        public RailPositionSaveData RailPosition { get; }
    }
    
    public interface ITrainCarPlacementDetector
    {
        bool TryDetect(ItemId holdingItemId, out TrainCarPlacementHit hit);
    }
    
    public class TrainCarPlacementDetector : ITrainCarPlacementDetector
    {
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

            // レール接続コライダーをレイキャストで取得する
            // Raycast to a rail connector collider
            if (!PlaceSystemUtil.TryGetRaySpecifiedComponentHitPosition<IRailComponentConnectAreaCollider>(_mainCamera,out var pos, out var connectArea, Without_Player_MapObject_BlockBoundingBox_LayerMask))
            {
                return false;
            }

            // 配置可否とレールスナップショットを解決する
            // Resolve placement validity and snapshot
            if (!TryBuildPlacement(connectArea, trainCarMaster, out hit))
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

            bool TryBuildPlacement(IRailComponentConnectAreaCollider connectArea, TrainCarMasterElement trainCarMasterElement, out TrainCarPlacementHit result)
            {
                result = default;
                // ブロック位置と接続情報を解決する
                // Resolve block position and connection info
                if (!TryResolveBlock(connectArea, out var blockPosition, out var railIndex, out var isStation))
                {
                    return false;
                }

                // 指定子とレール位置スナップショットを組み立てる
                // Compose specifier and rail position snapshot
                var destination = connectArea.CreateConnectionDestination();
                var specifier = isStation ? RailComponentSpecifier.CreateStationSpecifier(blockPosition, railIndex) : RailComponentSpecifier.CreateRailSpecifier(blockPosition);
                var previewPosition = blockPosition.AddBlockPlaceOffset();
                var previewRotation = Quaternion.identity;
                var trainLength = TrainLengthConverter.ToRailUnits(trainCarMasterElement.Length);
                var isPlaceable = TryBuildRailPosition(destination, trainLength, out var railPosition);
                result = new TrainCarPlacementHit(specifier, previewPosition, previewRotation, isPlaceable, railPosition);
                return true;

                #region Internal

                bool TryResolveBlock(IRailComponentConnectAreaCollider area, out Vector3Int position, out int index, out bool station)
                {
                    position = default;
                    index = 0;
                    station = false;
                    // レール種別ごとの座標を抽出する
                    // Extract block position by rail type
                    if (area is TrainRailConnectAreaCollider railArea)
                    {
                        position = railArea.BlockGameObject.BlockPosInfo.OriginalPos;
                        station = false;
                        return true;
                    }
                    if (area is StationRailConnectAreaCollider stationArea)
                    {
                        position = stationArea.BlockGameObject.BlockPosInfo.OriginalPos;
                        index = area.CreateConnectionDestination().railComponentID.ID;
                        station = true;
                        return true;
                    }
                    return false;
                }

                bool TryBuildRailPosition(ConnectionDestination headDestination, int length, out RailPositionSaveData railPositionSaveData)
                {
                    railPositionSaveData = null;
                    // 列車長と始点ノードを検証する
                    // Validate train length and head node
                    if (length <= 0)
                    {
                        return false;
                    }
                    // 始点ノードを確認する
                    // Check the head node
                    var headNode = _cache.ResolveRailNode(headDestination);
                    if (headNode == null || !_cache.TryGetNodeId(headNode, out var headNodeId))
                    {
                        return false;
                    }
                    // 進行方向の逆順でノードを積み上げる
                    // Build node list in reverse travel order
                    var railSnapshot = new List<ConnectionDestination> { headNode.ConnectionDestination };
                    var remaining = length;
                    var currentNodeId = headNodeId;
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

                    // 配置用スナップショットを生成する
                    // Create placement snapshot
                    railPositionSaveData = new RailPositionSaveData
                    {
                        TrainLength = length,
                        DistanceToNextNode = 0,
                        RailSnapshot = railSnapshot
                    };
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
                            // 対象ノードへの入力辺だけを抽出する
                            // Filter for incoming edges targeting the node
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

