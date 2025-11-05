using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.Common;
using Game.Train.Train;
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
        
        public InventoryRequestProtocol(ServiceProvider _) { }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // リクエストをデシリアライズ
            // Deserialize request
            var request = MessagePackSerializer.Deserialize<RequestInventoryRequestProtocolMessagePack>(payload.ToArray());
            
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
                
                if (block == null)
                {
                    return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Block, identifier, Array.Empty<IItemStack>(), -1);
                }
                
                // インベントリ要素を抽出
                // Collect inventory items
                var items = datastore.ExistsComponent<IOpenableBlockInventoryComponent>(position)
                    ? datastore.GetBlock<IOpenableBlockInventoryComponent>(position).InventoryItems
                    : Array.Empty<IItemStack>();
                
                return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Block, identifier, items, (int)block.BlockId);
            }
            
            ResponseInventoryRequestProtocolMessagePack CreateTrainResponse(InventoryIdentifierMessagePack identifier)
            {
                // 列車IDを解析
                // Parse train identifier
                if (!Guid.TryParse(identifier.TrainId, out var trainId))
                {
                    return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Train, identifier, Array.Empty<IItemStack>(), -1);
                }
                
                // 登録列車から対象を検索
                // Find target train from registered units
                var trainUnit = FindTrain(trainId);
                if (trainUnit == null)
                {
                    return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Train, identifier, Array.Empty<IItemStack>(), -1);
                }
                
                // 列車の各車両インベントリを収集
                // Gather inventory items from all cars
                var items = new List<IItemStack>();
                foreach (var car in trainUnit.Cars)
                {
                    items.AddRange(car.EnumerateInventory().Select(slot => slot.item));
                }
                
                return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Train, identifier, items, -1);
            }
            
            TrainUnit FindTrain(Guid trainId)
            {
                // 登録中の列車から一致するものを返却
                // Return matching train from registered units
                foreach (var train in TrainUpdateService.Instance.GetRegisteredTrains())
                {
                    if (train.TrainId == trainId) return train;
                }
                return null;
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
            [Key(5)] public int BlockId { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInventoryRequestProtocolMessagePack() { }
            
            public ResponseInventoryRequestProtocolMessagePack(InventoryType inventoryType, InventoryIdentifierMessagePack identifier, IReadOnlyList<IItemStack> items, int blockId)
            {
                Tag = ProtocolTag;
                InventoryType = inventoryType;
                Identifier = identifier;
                Items = items.Select(item => new ItemMessagePack(item)).ToArray();
                BlockId = blockId;
            }
            
            public ResponseInventoryRequestProtocolMessagePack(InventoryType inventoryType, InventoryIdentifierMessagePack identifier, IEnumerable<IItemStack> items, int blockId)
            {
                Tag = ProtocolTag;
                InventoryType = inventoryType;
                Identifier = identifier;
                Items = items.Select(item => new ItemMessagePack(item)).ToArray();
                BlockId = blockId;
            }
        }
    }
}
