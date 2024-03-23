using System;
using System.Collections.Generic;
using System.Linq;
using Core.Const;
using Game.Block.Interface.BlockConfig;
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
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly IEntityFactory _entityFactory;
        
        public RequestChunkDataProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestChunkDataMessagePack>(payload.ToArray());

            var result = new ChunkDataMessagePack[data.ChunkPos.Count];
            for (var i = 0; i < data.ChunkPos.Count; i++)
            {
                var chunkData = GetChunkData(data.ChunkPos[i]);
                result[i] = chunkData;
            }

            return new ResponseChunkDataMessagePack(result);

            #region Internal

            ChunkDataMessagePack GetChunkData(Vector2Int chunkPos)
            {
                var chunkOrigin = new Vector3Int(chunkPos.x, 0, chunkPos.y);
                var blocks = new List<BlockDataMessagePack>();
                
                for (int i = 0; i < ChunkResponseConst.ChunkSize; i++)
                for (int j = 0; j < ChunkResponseConst.ChunkSize; j++)
                {
                    var blockPos = chunkOrigin + new Vector3Int(i,0, j);
                    var originalPosBlock = _worldBlockDatastore.GetOriginPosBlock(blockPos);

                    if (originalPosBlock == null) continue;
                    
                    var blockDirection = originalPosBlock.BlockPositionInfo.BlockDirection;
                    var blockId = originalPosBlock.Block.BlockId;
                    blocks.Add(new BlockDataMessagePack(blockId, blockPos, blockDirection));
                }
                
                //TODO 今はベルトコンベアのアイテムをエンティティとして返しているだけ 今後は本当のentityも返す
                var items = CollectBeltConveyorItems.CollectItemFromChunk(chunkPos, _worldBlockDatastore, _blockConfig, _entityFactory);
                var entities = new List<EntityMessagePack>();
                entities.AddRange(items.Select(item => new EntityMessagePack(item)));

                return new ChunkDataMessagePack(chunkPos,blocks.ToArray(), entities.ToArray());
            }

            #endregion
        }
    }
    
    [MessagePackObject]
    public class RequestChunkDataMessagePack : ProtocolMessagePackBase
    {
        [Key(2)]
        public List<Vector2IntMessagePack> ChunkPos { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestChunkDataMessagePack()
        {
        }

        public RequestChunkDataMessagePack(List<Vector2IntMessagePack> chunkPos)
        {
            Tag = RequestChunkDataProtocol.Tag;
            ChunkPos = chunkPos;
        }
    }

    [MessagePackObject]
    public class ResponseChunkDataMessagePack : ProtocolMessagePackBase
    {
        [Key(2)]
        public ChunkDataMessagePack[] ChunkData { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseChunkDataMessagePack()
        {
        }

        public ResponseChunkDataMessagePack(ChunkDataMessagePack[] chunkData)
        {
            Tag = RequestChunkDataProtocol.Tag;
            ChunkData = chunkData;
        }
    }
    
    [MessagePackObject]
    public class ChunkDataMessagePack
    {
        [Key(0)]
        public Vector2IntMessagePack ChunkPos { get; set; }
        [Key(1)]
        public BlockDataMessagePack[] Blocks { get; set; }
        [Key(2)]
        public EntityMessagePack[] Entities { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChunkDataMessagePack()
        {
        }

        public ChunkDataMessagePack(Vector2Int chunkPos,BlockDataMessagePack[] blocks, EntityMessagePack[] entities)
        {
            ChunkPos = new Vector2IntMessagePack(chunkPos);
            Blocks = blocks;
            Entities = entities;
        }
    }
}