using System;
using System.Collections.Generic;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class GetChunkDataProtocol : IPacketResponse
    {
        public GetChunkDataProtocol(ServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }

        public const string Tag = "va:getChunk";


        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestChunkDataMessagePack>(payload.ToArray());

            var response = new List<List<byte>>();
            foreach (var chunkPos in data.ChunkPos)
            {
                var chunkData = GetChunkData(chunkPos.Vector2Int);
                response.Add(chunkData);
            }

            return response;

            #region Internal

            List<byte> GetChunkData(Vector2Int chunkPos)
            {
                throw new NotImplementedException();
            }

            #endregion
        }
        
        
        
    }
    
    [MessagePackObject(true)]
    public class RequestChunkDataMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestChunkDataMessagePack()
        {
        }

        public RequestChunkDataMessagePack(List<Vector2IntMessagePack> chunkPos)
        {
            Tag = GetChunkDataProtocol.Tag;
            ChunkPos = chunkPos;
        }

        public List<Vector2IntMessagePack> ChunkPos { get; set; }
    }

    [MessagePackObject(true)]
    public class ResponseChunkDataMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseChunkDataMessagePack()
        {
        }

        public ResponseChunkDataMessagePack(ChunkDataMessagePack[] chunkData)
        {
            Tag = GetChunkDataProtocol.Tag;
            ChunkData = chunkData;
        }
        
        public ChunkDataMessagePack[] ChunkData { get; set; }
    }
    
    [MessagePackObject(true)]
    public class ChunkDataMessagePack
    {
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChunkDataMessagePack()
        {
        }

        public ChunkDataMessagePack(Vector2Int chunkPos, int[,] blockIds, int[,] blockDirections, EntityMessagePack[] entities)
        {
            ChunkPos = new Vector2IntMessagePack(chunkPos);
            BlockIds = blockIds;
            BlockDirections = blockDirections;
            Entities = entities;
        }
        
        public Vector2IntMessagePack ChunkPos { get; set; }
        public int[,] BlockIds { get; set; }
        public int[,] BlockDirections { get; set; }
        public EntityMessagePack[] Entities { get; set; }
        
    }
}