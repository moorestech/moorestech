using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Entity.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Util;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RequestWorldDataProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getWorldData";
        public const float ItemVisibilityDistance = 10f;

        private readonly IEntityFactory _entityFactory;
        private readonly IEntitiesDatastore _entitiesDatastore;

        public RequestWorldDataProtocol(ServiceProvider serviceProvider)
        {
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
            _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // リクエストからPlayerIdを取得
            // Get PlayerId from request
            var request = MessagePackSerializer.Deserialize<RequestWorldDataMessagePack>(payload.ToArray());

            // プレイヤー位置を取得
            // Get player position
            var playerEntityId = new EntityInstanceId(request.PlayerId);
            var playerPosition = _entitiesDatastore.Exists(playerEntityId)
                ? _entitiesDatastore.GetPosition(playerEntityId)
                : Vector3.zero;

            // ブロック収集（既存処理）
            // Collect blocks (existing logic)
            var blockMasterDictionary = ServerContext.WorldBlockDatastore.BlockMasterDictionary;
            var blockResult = new List<BlockDataMessagePack>();
            foreach (var blockMaster in blockMasterDictionary)
            {
                var block = blockMaster.Value.Block;
                var pos = blockMaster.Value.BlockPositionInfo.OriginalPos;
                var blockDirection = blockMaster.Value.BlockPositionInfo.BlockDirection;
                blockResult.Add(new BlockDataMessagePack(block.BlockId, pos, blockDirection, block.BlockInstanceId));
            }

            // エンティティ収集（距離フィルタリング付き）
            // Collect entities with distance filtering
            var entities = new List<EntityMessagePack>();
            var items = CollectBeltConveyorItems.CollectItemFromWorld(_entityFactory, playerPosition, ItemVisibilityDistance);
            entities.AddRange(items.Select(item => new EntityMessagePack(item)));

            return new ResponseWorldDataMessagePack(blockResult.ToArray(), entities.ToArray());
        }


        [MessagePackObject]
        public class RequestWorldDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }

            public RequestWorldDataMessagePack(int playerId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
            }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestWorldDataMessagePack() { }
        }
        
        [MessagePackObject]
        public class ResponseWorldDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockDataMessagePack[] Blocks { get; set; }
            [Key(3)] public EntityMessagePack[] Entities { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseWorldDataMessagePack() { }
            public ResponseWorldDataMessagePack(BlockDataMessagePack[] Block, EntityMessagePack[] entities)
            {
                Tag = ProtocolTag;
                Blocks = Block;
                Entities = entities;
            }
        }
    }
}
