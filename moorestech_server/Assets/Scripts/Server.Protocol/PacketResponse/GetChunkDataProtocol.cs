using System;
using System.Collections.Generic;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class GetChunkDataProtocol : IPacketResponse
    {
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

        public RequestChunkDataMessagePack(int playerId, string playerName)
        {
            Tag = GetChunkDataProtocol.Tag;
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

        public ResponseChunkDataMessagePack(Vector2Int chunk, int[,] blockIds, int[,] blockDirect,EntityMessagePack[] entities)
        {
            Tag = GetChunkDataProtocol.Tag;
            ChunkPos = new Vector2IntMessagePack(chunk);
            BlockIds = blockIds;
            BlockDirect = blockDirect;
            Entities = entities;
        }
        
        public Vector2IntMessagePack ChunkPos { get; set; }
        
        public int[,] BlockIds { get; set; }
        public int[,] BlockDirect { get; set; }
        
        public EntityMessagePack[] Entities { get; set; }
    }
}