using System;
using Game.World.Interface.DataStore;

namespace Server.Protocol.PacketResponse.MessagePack
{
    public class EntityResponseMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EntityResponseMessagePack() { }

        public EntityResponseMessagePack(Coordinate chunk, int[,] blockIds, int[,] blockDirect, int[,] mapTileIds)
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