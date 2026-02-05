using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RailConnectWithPlacePierProtocol : IPacketResponse
    {
        public const string Tag = "va:railConnectWithPlacePier";
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly RailConnectionCommandHandler _commandHandler;
        
        public RailConnectWithPlacePierProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _railGraphDatastore = serviceProvider.GetService<IRailGraphDatastore>();
            _commandHandler = serviceProvider.GetService<RailConnectionCommandHandler>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<RailConnectWithPlacePierRequest>(payload.ToArray());
            var inventoryData = _playerInventoryDataStore.GetInventoryData(request.PlayerId);
            
            // fromNodeの取得
            if (!_railGraphDatastore.TryGetRailNode(request.FromNodeId, out var fromNode) || fromNode == null || fromNode.Guid != request.FromGuid) return RailConnectWithPlacePierResponse.CreateFailedResponse();
            
            // すでにブロックがある場合はそのまま処理を終了
            if (ServerContext.WorldBlockDatastore.Exists(request.PierPlaceInfo.Position)) return RailConnectWithPlacePierResponse.CreateFailedResponse();
            
            // アイテムIDがブロックIDに変換できない場合はそのまま処理を終了
            var itemStack = inventoryData.MainOpenableInventory.GetItem(request.PierInventorySlot);
            if (!MasterHolder.BlockMaster.IsBlock(itemStack.Id))  return RailConnectWithPlacePierResponse.CreateFailedResponse();
            
            // ブロックIDの設定
            var blockId = MasterHolder.BlockMaster.GetBlockId(itemStack.Id);
            blockId = blockId.GetVerticalOverrideBlockId(request.PierPlaceInfo.VerticalDirection);
            
            // paramsの設定
            BlockCreateParam[] createParams = request.PierPlaceInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();
            
            // ブロックの設置
            ServerContext.WorldBlockDatastore.TryAddBlock(blockId, request.PierPlaceInfo.Position, request.PierPlaceInfo.Direction, createParams, out var block);
            var railComponent = block.GetComponent<RailComponent>();
            var toNode = railComponent.BackNode;
            
            if (!RailConnectionEditProtocol.TryResolveRailItemForPlacement(request.RailTypeGuid, inventoryData.MainOpenableInventory.InventoryItems, RailConnectionEditProtocol.GetRailLength(fromNode, toNode), out var railItem, out var requiredCount))  return RailConnectWithPlacePierResponse.CreateFailedResponse();
            
            var connectResult = _commandHandler.TryConnect(fromNode.NodeId, fromNode.Guid, toNode.NodeId, toNode.Guid, request.RailTypeGuid);
            
            if (connectResult && request.RailTypeGuid != Guid.Empty)
            {
                var railItemId = MasterHolder.ItemMaster.GetItemId(railItem.ItemGuid);
                var remainSubCount = requiredCount;
                foreach (var (stack, i) in inventoryData.MainOpenableInventory.InventoryItems.ToArray().Select((stack, i) => (stack, i)).Where(stack => stack.stack.Id == railItemId))
                {
                    var subCount = Mathf.Min(stack.Count, remainSubCount);
                    Debug.Log($"Subtracting {subCount} from stack {stack.Id}");
                    inventoryData.MainOpenableInventory.SetItem(i, stack.SubItem(subCount));
                    remainSubCount -= subCount;
                    
                    if (remainSubCount == 0) break;
                }
                
                // 残りがまだあるということがあってはならない
                if (remainSubCount > 0) throw new Exception($"Rail item count is not enough. Required: {requiredCount}, Inventory: {inventoryData.MainOpenableInventory.InventoryItems.Where(stack => stack.Id == railItemId).Sum(stack => stack.Count)}");
            }
            
            // アイテムを減らし、セットする
            itemStack = itemStack.SubItem(1);
            inventoryData.MainOpenableInventory.SetItem(request.PierInventorySlot, itemStack);
            
            return RailConnectWithPlacePierResponse.Create(toNode.NodeId, toNode.Guid);
        }
        
        [MessagePackObject]
        public class RailConnectWithPlacePierResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool Success { get; set; }
            [Key(3)] public int ToNodeId { get; set; }
            [Key(4)] public Guid ToNodeGuid { get; set; }
            
            public RailConnectWithPlacePierResponse()
            {
                Tag = RailConnectWithPlacePierProtocol.Tag;
            }
            
            public static RailConnectWithPlacePierResponse CreateFailedResponse()
            {
                return new RailConnectWithPlacePierResponse()
                {
                    Success = false
                };
            }
            
            public static RailConnectWithPlacePierResponse Create(int toNodeId, Guid toNodeGuid)
            {
                return new RailConnectWithPlacePierResponse()
                {
                    Success = true,
                    ToNodeId = toNodeId,
                    ToNodeGuid = toNodeGuid
                };
            }
        }
        
        [MessagePackObject]
        public class RailConnectWithPlacePierRequest : ProtocolMessagePackBase
        {
            [Key(2)] public int FromNodeId { get; set; }
            [Key(3)] public Guid FromGuid { get; set; }
            [Key(4)] public PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack PierPlaceInfo
            {
                get;
                set;
            }
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public int PierInventorySlot { get; set; }
            [Key(7)] public Guid RailTypeGuid { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailConnectWithPlacePierRequest()
            {
                Tag = RailConnectWithPlacePierProtocol.Tag;
            }
            
            public static RailConnectWithPlacePierRequest Create(int playerId, int fromNodeId, Guid fromGuid, int pierInventorySlot, PlaceInfo placeInfo, Guid railTypeGuid)
            {
                return new RailConnectWithPlacePierRequest
                {
                    PlayerId = playerId,
                    FromNodeId = fromNodeId,
                    FromGuid = fromGuid,
                    PierInventorySlot = pierInventorySlot,
                    RailTypeGuid = railTypeGuid,
                    PierPlaceInfo = new PlaceBlockFromHotBarProtocol.PlaceInfoMessagePack(placeInfo),
                };
            }
        }
    }
}