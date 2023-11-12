using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.World.Interface.DataStore;
using MainGame.Basic;
using MainGame.Network.Event;
using MessagePack;
using Server.Protocol.PacketResponse.MessagePack;
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
            var blockDirections = new BlockDirection[ChunkConstant.ChunkSize, ChunkConstant.ChunkSize];

            //analysis block data
            for (var i = 0; i < ChunkConstant.ChunkSize; i++)
            for (var j = 0; j < ChunkConstant.ChunkSize; j++)
                blockDirections[i, j] = (BlockDirection)data.BlockDirect[i, j];

            //chunk data event
            receiveChunkDataEvent.InvokeChunkUpdateEvent(new ChunkUpdateEventProperties(
                chunkPos, data.BlockIds, blockDirections, data.MapTileIds)).Forget();
        }
    }
}