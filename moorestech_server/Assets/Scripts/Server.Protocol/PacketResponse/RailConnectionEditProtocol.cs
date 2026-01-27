using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.PlayerInventory.Interface;
using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.TrainModule;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectionEditProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectionEdit";

        private readonly RailConnectionCommandHandler _commandHandler;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainRailPositionManager _railPositionManager;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public RailConnectionEditProtocol(ServiceProvider serviceProvider)
        {
            _commandHandler = serviceProvider.GetService<RailConnectionCommandHandler>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _railPositionManager = serviceProvider.GetService<TrainRailPositionManager>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RailConnectionEditRequest>(payload.ToArray());

            // 編集処理を実行
            // Execute edit operation
            return ExecuteEdit(request);

            #region Internal

            ResponseRailConnectionEditMessagePack ExecuteEdit(RailConnectionEditRequest data)
            {
                if (_commandHandler == null)
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.UnknownError, data.Mode);
                }

                // モードに応じて接続または切断を実行する
                // Execute connect or disconnect depending on mode
                switch (data.Mode)
                {
                    case RailEditMode.Connect:
                        if (!_commandHandler.TryResolveNodes(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid, out var fromNode, out var toNode))
                            return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);
                        
                        var length = GetRailLength(fromNode, toNode);
                        
                        var inventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
                        (RailItemMasterElement element, int requiredCount)[] placeableRailItems = GetPlaceableRailItems(inventory, length);
                        
                        if (placeableRailItems.Length == 0) return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NotEnoughRailItem, data.Mode);
                        var placeRailItem = placeableRailItems[0];
                        Debug.Log($"Placeable rail items: {placeableRailItems.Length}, Place rail item: {placeRailItem.element.ItemGuid}, Required count: {placeRailItem.requiredCount}");
                        
                        var connectResult = _commandHandler.TryConnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                        
                        // 成功したらインベントリから引く
                        if (connectResult)
                        {
                            var railItemId = MasterHolder.ItemMaster.GetItemId(placeRailItem.element.ItemGuid);
                            var remainSubCount = placeRailItem.requiredCount;
                            foreach (var (stack, i) in inventory.InventoryItems.Select((stack, i) => (stack, i)).Where(stack => stack.stack.Id == railItemId))
                            {
                                var subCount = Mathf.Min(stack.Count, remainSubCount);
                                Debug.Log($"Subtracting {subCount} from stack {stack.Id}");
                                inventory.SetItem(i, stack.SubItem(subCount));
                                remainSubCount -= subCount;
                                
                                if (remainSubCount == 0) break;
                            }
                            
                            // 残りがまだあるということがあってはならない
                            if (remainSubCount > 0) throw new Exception($"Rail item count is not enough. Required: {placeRailItem.requiredCount}, Inventory: {inventory.InventoryItems.Where(stack => stack.Id == railItemId).Sum(stack => stack.Count)}");
                        }
                        
                        return ResponseRailConnectionEditMessagePack.Create(connectResult, connectResult ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.InvalidNode, data.Mode);
                    case RailEditMode.Disconnect:
                        return HandleDisconnect(data);
                }

                return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidMode, data.Mode);
            }

            ResponseRailConnectionEditMessagePack HandleDisconnect(RailConnectionEditRequest data)
            {
                if (!_railGraphDatastore.TryGetRailNode(data.FromNodeId, out var fromNode) || fromNode == null || fromNode.Guid != data.FromGuid)
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);
                }

                if (!_railGraphDatastore.TryGetRailNode(data.ToNodeId, out var toNode) || toNode == null || toNode.Guid != data.ToGuid)
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.InvalidNode, data.Mode);
                }

                if (IsStationInternalEdge(fromNode, toNode))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.StationInternalEdge, data.Mode);
                }

                if (!_railPositionManager.CanRemoveEdge(fromNode, toNode))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NodeInUseByTrain, data.Mode);
                }
                if (!_railPositionManager.CanRemoveEdge(toNode.OppositeRailNode, fromNode.OppositeRailNode))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NodeInUseByTrain, data.Mode);
                }

                var disconnected = _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                return ResponseRailConnectionEditMessagePack.Create(disconnected, disconnected ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.UnknownError, data.Mode);
            }

            bool IsStationInternalEdge(RailNode from, RailNode to)
            {
                if (!from.StationRef.HasStation || !to.StationRef.HasStation)
                {
                    return false;
                }
                return from.StationRef.StationBlockInstanceId.Equals(to.StationRef.StationBlockInstanceId);
            }

            #endregion
        }
        
        public static float GetRailLength(IRailNode fromNode, IRailNode toNode)
        {
            var p0 = fromNode.FrontControlPoint.OriginalPosition;
            var p1 = fromNode.FrontControlPoint.OriginalPosition + fromNode.FrontControlPoint.ControlPointPosition;
            var p2 = toNode.BackControlPoint.OriginalPosition + toNode.BackControlPoint.ControlPointPosition;
            var p3 = toNode.BackControlPoint.OriginalPosition;
            var length = BezierUtility.GetBezierCurveLength(p0, p1, p2, p3, 64);
            
            return length;
        }
        
        public static (RailItemMasterElement element, int requiredCount)[] GetPlaceableRailItems(IOpenableInventory inventory, float railLength)
        {
            var placeableRailItems = new List<(RailItemMasterElement element, int requiredCount)>();
            foreach (var railMasterElement in MasterHolder.TrainUnitMaster.GetRailItems())
            {
                // 設置に必要なアイテム数
                var requiredCount = Mathf.CeilToInt(railLength * railMasterElement.ConsumptionPerUnitLength);
                
                // inventoryにその分があるならplaceableRailItemsに追加
                var railItemId = MasterHolder.ItemMaster.GetItemId(railMasterElement.ItemGuid);
                var ownedRailItemCount = inventory.InventoryItems.Where(stack => stack.Id == railItemId).Sum(stack => stack.Count);
                if (ownedRailItemCount >= requiredCount)
                {
                    placeableRailItems.Add((railMasterElement, requiredCount));
                }
            }
            
            return placeableRailItems.ToArray();
        }

        [MessagePackObject]
        public class RailConnectionEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public int FromNodeId { get; set; }
            [Key(3)] public Guid FromGuid { get; set; }
            [Key(4)] public int ToNodeId { get; set; }
            [Key(5)] public Guid ToGuid { get; set; }
            [Key(6)] public RailEditMode Mode { get; set; }
            [Key(7)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest() { Tag = RailConnectionEditProtocol.Tag; }
            
            public static RailConnectionEditRequest CreateConnectRequest(int playerId, int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
            {
                return new RailConnectionEditRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Connect,
                };
            }

            public static RailConnectionEditRequest CreateDisconnectRequest(int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
            {
                return new RailConnectionEditRequest
                {
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Disconnect,
                };
            }
        }

        [MessagePackObject]
        public class ResponseRailConnectionEditMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public RailConnectionEditFailureReason FailureReason { get; set; }
            [Key(4)] public RailEditMode Mode { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseRailConnectionEditMessagePack()
            {
                Tag = RailConnectionEditProtocol.Tag;
            }

            public static ResponseRailConnectionEditMessagePack Create(bool success, RailConnectionEditFailureReason reason, RailEditMode mode)
            {
                return new ResponseRailConnectionEditMessagePack
                {
                    Success = success,
                    FailureReason = reason,
                    Mode = mode,
                };
            }

            public static ResponseRailConnectionEditMessagePack CreateFailure(RailConnectionEditFailureReason reason, RailEditMode mode)
            {
                return Create(false, reason, mode);
            }
        }

        public enum RailEditMode
        {
            Connect,
            Disconnect,
        }

        public enum RailConnectionEditFailureReason
        {
            None,
            InvalidNode,
            NodeInUseByTrain,
            StationInternalEdge,
            InvalidMode,
            NotEnoughRailItem,
            UnknownError,
        }
    }
}

