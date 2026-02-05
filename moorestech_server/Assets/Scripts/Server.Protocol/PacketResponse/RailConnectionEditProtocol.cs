using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
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
                        var railTypeGuid = data.RailTypeGuid;
                        if (!TryResolveRailItemForPlacement(railTypeGuid, inventory.InventoryItems, length, out var placeRailItem, out var requiredCount))
                            return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NotEnoughRailItem, data.Mode);

                        var connectResult = _commandHandler.TryConnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid, railTypeGuid);
                        
                        // 成功したらインベントリから引く
                        // Consume rail items on success
                        if (connectResult && railTypeGuid != Guid.Empty)
                        {
                            var railItemId = MasterHolder.ItemMaster.GetItemId(placeRailItem.ItemGuid);
                            var remainSubCount = requiredCount;
                            foreach (var (stack, i) in inventory.InventoryItems.ToArray().Select((stack, i) => (stack, i)).Where(stack => stack.stack.Id == railItemId))
                            {
                                var subCount = Mathf.Min(stack.Count, remainSubCount);
                                Debug.Log($"Subtracting {subCount} from stack {stack.Id}");
                                inventory.SetItem(i, stack.SubItem(subCount));
                                remainSubCount -= subCount;
                                
                                if (remainSubCount == 0) break;
                            }
                            
                            // 残りがまだあるということがあってはならない
                            if (remainSubCount > 0) throw new Exception($"Rail item count is not enough. Required: {requiredCount}, Inventory: {inventory.InventoryItems.Where(stack => stack.Id == railItemId).Sum(stack => stack.Count)}");
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
                
                var railTypeGuid = ResolveRailTypeGuid(data.FromNodeId, data.ToNodeId);
                var inventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
                var railLength = GetRailLength(fromNode, toNode);
                if (railTypeGuid == Guid.Empty)
                {
                    var disconnected = _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                    return ResponseRailConnectionEditMessagePack.Create(disconnected, disconnected ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.UnknownError, data.Mode);
                }

                if (!MasterHolder.TrainUnitMaster.TryGetRailItem(railTypeGuid, out var railItem))
                {
                    var disconnected = _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                    return ResponseRailConnectionEditMessagePack.Create(disconnected, disconnected ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.UnknownError, data.Mode);
                }

                var requiredCount = CalculateRailItemRequiredCount(railLength, railItem);
                var itemStack = ServerContext.ItemStackFactory.Create(railItem.ItemGuid, requiredCount);
                
                // playerインベントリに空きがない場合は削除不可
                // Abort when there is no inventory space to return the item
                if (!inventory.InsertionCheck(new List<IItemStack> { itemStack }))
                {
                    return ResponseRailConnectionEditMessagePack.CreateFailure(RailConnectionEditFailureReason.NotEnoughInventorySpace, data.Mode);
                }

                var disconnectedflag = _commandHandler.TryDisconnect(data.FromNodeId, data.FromGuid, data.ToNodeId, data.ToGuid);
                
                // アイテムを返却
                // Return rail items
                if (disconnectedflag)
                {
                    inventory.InsertItem(itemStack);
                }
                
                return ResponseRailConnectionEditMessagePack.Create(disconnectedflag, disconnectedflag ? RailConnectionEditFailureReason.None : RailConnectionEditFailureReason.UnknownError, data.Mode);
            }

            bool IsStationInternalEdge(RailNode from, RailNode to)
            {
                if (!from.StationRef.HasStation || !to.StationRef.HasStation)
                {
                    return false;
                }
                return from.StationRef.StationBlockInstanceId.Equals(to.StationRef.StationBlockInstanceId);
            }

            // レール種別をセグメントから解決する
            // Resolve rail type from the segment data
            Guid ResolveRailTypeGuid(int fromNodeId, int toNodeId)
            {
                return _railGraphDatastore.TryGetRailSegmentType(fromNodeId, toNodeId, out var railTypeGuid) ? railTypeGuid : Guid.Empty;
            }

            #endregion
        }
        
        /// レール設置用のアイテム情報を解決する
        /// Resolve rail item information for placement
        public static bool TryResolveRailItemForPlacement(Guid railTypeGuid, IEnumerable<IItemStack> inventoryItems, float railLength, out RailItemMasterElement railItem, out int requiredCount)
        {
            railItem = default;
            requiredCount = 0;
            if (railTypeGuid == Guid.Empty)
            {
                return true;
            }
            
            if (!MasterHolder.TrainUnitMaster.TryGetRailItem(railTypeGuid, out railItem))
            {
                return false;
            }
            
            requiredCount = CalculateRailItemRequiredCount(railLength, railItem);
            var railItemId = MasterHolder.ItemMaster.GetItemId(railItem.ItemGuid);
            var ownedCount = inventoryItems.Where(stack => stack.Id == railItemId).Sum(stack => stack.Count);
            if (ownedCount < requiredCount)
            {
                return false;
            }
            
            Debug.Log($"Place rail item: {railItem.ItemGuid}, Required count: {requiredCount}");
            return true;
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
        
        public static (RailItemMasterElement element, int requiredCount)[] GetPlaceableRailItems(IEnumerable<IItemStack> inventory, float railLength)
        {
            var placeableRailItems = new List<(RailItemMasterElement element, int requiredCount)>();
            IItemStack[] itemStacks = inventory as IItemStack[] ?? inventory.ToArray();
            foreach (var railMasterElement in MasterHolder.TrainUnitMaster.GetRailItems())
            {
                // 設置に必要なアイテム数
                var requiredCount = CalculateRailItemRequiredCount(railLength, railMasterElement);
                
                // inventoryにその分があるならplaceableRailItemsに追加
                var railItemId = MasterHolder.ItemMaster.GetItemId(railMasterElement.ItemGuid);
                var ownedRailItemCount = itemStacks.Where(stack => stack.Id == railItemId).Sum(stack => stack.Count);
                if (ownedRailItemCount >= requiredCount)
                {
                    placeableRailItems.Add((railMasterElement, requiredCount));
                }
            }
            
            return placeableRailItems.ToArray();
        }
        
        private static int CalculateRailItemRequiredCount(float railLength, RailItemMasterElement railMasterElement)
        {
            return Mathf.CeilToInt(railLength * railMasterElement.ConsumptionPerUnitLength);
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
            [Key(8)] public Guid RailTypeGuid { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectionEditRequest() { Tag = RailConnectionEditProtocol.Tag; }
            
            public static RailConnectionEditRequest CreateConnectRequest(int playerId, int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid, Guid railTypeGuid)
            {
                return new RailConnectionEditRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Connect,
                    RailTypeGuid = railTypeGuid,
                };
            }
            
            public static RailConnectionEditRequest CreateDisconnectRequest(int playerId, int fromNodeId, Guid fromGuid, int toNodeId, Guid toGuid)
            {
                return new RailConnectionEditRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    ToNodeId = toNodeId,
                    ToGuid = toGuid,
                    Mode = RailEditMode.Disconnect,
                    RailTypeGuid = Guid.Empty,
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
            NotEnoughInventorySpace,
            UnknownError,
        }
    }
}

