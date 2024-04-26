using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface.BlockConfig;
using Game.Context;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Const;
using Server.Protocol.PacketResponse.Util;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RequestChunkDataProtocol : IPacketResponse
    {
        public const string Tag = "va:getChunk";
        private readonly IEntityFactory _entityFactory;

        public RequestChunkDataProtocol(ServiceProvider serviceProvider)
        {
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestChunkDataMessagePack>(payload.ToArray());

            var result = new ChunkDataMessagePack[1];

            //TODO 創風仮対応 そのうちチャンクの概念を消す

            //TODO ブロックを全部取得して変換
            var blockMasterDictionary = ServerContext.WorldBlockDatastore.BlockMasterDictionary;
            var blockResult = new List<BlockDataMessagePack>();
            foreach (var blockMaster in blockMasterDictionary)
            {
                var block = blockMaster.Value.Block;
                var pos = blockMaster.Value.BlockPositionInfo.OriginalPos;
                var blockDirection = blockMaster.Value.BlockPositionInfo.BlockDirection;
                blockResult.Add(new BlockDataMessagePack(block.BlockId, pos, blockDirection));
            }


            //TODO 今はベルトコンベアのアイテムをエンティティとして返しているだけ 今後は本当のentityも返す
            List<IEntity> items = CollectBeltConveyorItems.CollectItemFromChunk(_entityFactory);
            var entities = new List<EntityMessagePack>();
            entities.AddRange(items.Select(item => new EntityMessagePack(item)));


            result[0] = new ChunkDataMessagePack(new Vector2Int(0, 0), blockResult.ToArray(), entities.ToArray());
            //TODO ここまで仮対応

            return new ResponseChunkDataMessagePack(result);
        }
    }

    [MessagePackObject]
    public class RequestChunkDataMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestChunkDataMessagePack()
        {
        }

        public RequestChunkDataMessagePack(List<Vector2IntMessagePack> chunkPos)
        {
            Tag = RequestChunkDataProtocol.Tag;
            ChunkPos = chunkPos;
        }
        [Key(2)]
        public List<Vector2IntMessagePack> ChunkPos { get; set; }
    }

    [MessagePackObject]
    public class ResponseChunkDataMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseChunkDataMessagePack()
        {
        }

        public ResponseChunkDataMessagePack(ChunkDataMessagePack[] chunkData)
        {
            Tag = RequestChunkDataProtocol.Tag;
            ChunkData = chunkData;
        }
        [Key(2)]
        public ChunkDataMessagePack[] ChunkData { get; set; }
    }

    [MessagePackObject]
    public class ChunkDataMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChunkDataMessagePack()
        {
        }

        public ChunkDataMessagePack(Vector2Int chunkPos, BlockDataMessagePack[] blocks, EntityMessagePack[] entities)
        {
            ChunkPos = new Vector2IntMessagePack(chunkPos);
            Blocks = blocks;
            Entities = entities;
        }
        [Key(0)]
        public Vector2IntMessagePack ChunkPos { get; set; }
        [Key(1)]
        public BlockDataMessagePack[] Blocks { get; set; }
        [Key(2)]
        public EntityMessagePack[] Entities { get; set; }
    }
}