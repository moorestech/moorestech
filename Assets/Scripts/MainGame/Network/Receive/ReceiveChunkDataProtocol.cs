using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MainGame.Basic;
using MainGame.Network.Event;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network.Receive
{
    public class ReceiveChunkDataProtocol : IAnalysisPacket
    {
        private readonly ReceiveChunkDataEvent receiveChunkDataEvent;

        public ReceiveChunkDataProtocol(ReceiveChunkDataEvent receiveChunkDataEvent)
        {
            this.receiveChunkDataEvent = receiveChunkDataEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<ChunkDataResponseMessagePack>(packet.ToArray());
            
            //packet id
            var chunkPos = new Vector2Int(data.ChunkX, data.ChunkY);
            var blockDirections = new BlockDirection[ChunkConstant.ChunkSize,ChunkConstant.ChunkSize];
            
            //analysis block data
            for (int i = 0; i < ChunkConstant.ChunkSize; i++)
            {
                for (int j = 0; j < ChunkConstant.ChunkSize; j++)
                {
                    blockDirections[i, j] = (BlockDirection)data.BlockDirect[i,j];
                }
            }

            //chunk data event
            receiveChunkDataEvent.InvokeChunkUpdateEvent(new ChunkUpdateEventProperties(
                chunkPos,  data.BlockIds,blockDirections,data.MapTileIds)).Forget();
        }
    }
}