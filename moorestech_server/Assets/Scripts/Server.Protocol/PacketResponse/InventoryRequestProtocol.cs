using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Unit;

using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using Game.World.Interface.DataStore;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// インベントリ取得プロトコル
    /// Inventory fetch protocol
    /// </summary>
    public class InventoryRequestProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:invReq";
        
        private readonly TrainUpdateService _trainUpdateService;
        
        public InventoryRequestProtocol(ServiceProvider serviceProvider)
        {
            _trainUpdateService = serviceProvider.GetService<TrainUpdateService>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            // リクエストをデシリアライズ
            // Deserialize request
            var request = MessagePackSerializer.Deserialize<RequestInventoryRequestProtocolMessagePack>(payload);
            
            // 識別子ごとのインベントリを判定取得
            // Dispatch inventory retrieval by identifier type
            return request.Identifier.InventoryType switch
            {
                InventoryType.Block => CreateBlockResponse(request.Identifier),
                InventoryType.Train => CreateTrainResponse(request.Identifier),
                _ => throw new ArgumentException($"Unknown inventory type: {request.Identifier.InventoryType}")
            };
            
            #region Internal
            
            ResponseInventoryRequestProtocolMessagePack CreateBlockResponse(InventoryIdentifierMessagePack identifier)
            {
                // ブロック情報を取得
                // Fetch block information
                var position = identifier.BlockPosition.Vector3Int;
                var datastore = ServerContext.WorldBlockDatastore;
                var block = datastore.GetBlock(position);
                
                if (block == null)  return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Block, identifier, Array.Empty<IItemStack>());
                
                // インベントリ要素を抽出
                // Collect inventory items
                var items = datastore.ExistsComponent<IOpenableBlockInventoryComponent>(position)
                    ? datastore.GetBlock<IOpenableBlockInventoryComponent>(position).InventoryItems
                    : Array.Empty<IItemStack>();
                
                return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Block, identifier, items);
            }
            
            ResponseInventoryRequestProtocolMessagePack CreateTrainResponse(InventoryIdentifierMessagePack identifier)
            {
                // 列車カーを探索
                // Find the target train car
                var trainCarInstanceId = new TrainCarInstanceId(long.Parse(identifier.TrainCarInstanceId));
                TrainCar trainCar = null;
                foreach (var registeredTrain in _trainUpdateService.GetRegisteredTrains())
                {
                    foreach (var car in registeredTrain.Cars)
                    {
                        if (car.TrainCarInstanceId != trainCarInstanceId) continue;
                        trainCar = car;
                        break;
                    }
                }
                
                if (trainCar == null) return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Train, identifier, Array.Empty<IItemStack>());
                
                // 列車カーのインベントリを生成
                // Build the train car inventory
                var items = trainCar.EnumerateInventory().Select(slot => new ItemMessagePack(slot.item)).ToArray();
                return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Train, identifier, items);
            }
            
            #endregion
        }
        
        
        [MessagePackObject]
        public class RequestInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public InventoryIdentifierMessagePack Identifier { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestInventoryRequestProtocolMessagePack() { }
            
            public RequestInventoryRequestProtocolMessagePack(InventoryIdentifierMessagePack identifier)
            {
                Tag = ProtocolTag;
                Identifier = identifier;
            }
        }
        
        [MessagePackObject]
        public class ResponseInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public InventoryType InventoryType { get; set; }
            [Key(3)] public InventoryIdentifierMessagePack Identifier { get; set; }
            [Key(4)] public ItemMessagePack[] Items { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInventoryRequestProtocolMessagePack() { }
            
            public ResponseInventoryRequestProtocolMessagePack(InventoryType inventoryType, InventoryIdentifierMessagePack identifier, IReadOnlyList<IItemStack> items)
            {
                Tag = ProtocolTag;
                InventoryType = inventoryType;
                Identifier = identifier;
                Items = items.Select(item => new ItemMessagePack(item)).ToArray();
            }
            
            
            public ResponseInventoryRequestProtocolMessagePack(InventoryType inventoryType, InventoryIdentifierMessagePack identifier, ItemMessagePack[] items)
            {
                Tag = ProtocolTag;
                InventoryType = inventoryType;
                Identifier = identifier;
                Items = items;
            }
        }
    }
}
