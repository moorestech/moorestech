using System;
using Game.World.Interface.DataStore;
using MessagePack;

namespace Server.Protocol.PacketResponse.MessagePack
{
    [MessagePackObject(true)]
    public class ChunkDataResponseMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public ChunkDataResponseMessagePack()
        {
        }

        public ChunkDataResponseMessagePack(Coordinate chunk, int[,] blockIds, int[,] blockDirect, int[,] mapTileIds)
        {
            Tag = PlayerCoordinateSendProtocol.ChunkDataTag;
            ChunkX = chunk.X;
            ChunkY = chunk.Y;
            BlockIds = blockIds;
            BlockDirect = blockDirect;
            MapTileIds = mapTileIds;
        }

        public int ChunkX { get; set; }
        public int ChunkY { get; set; }

        public int[,] BlockIds { get; set; }
        public int[,] BlockDirect { get; set; }
        public int[,] MapTileIds { get; set; }
    }
}