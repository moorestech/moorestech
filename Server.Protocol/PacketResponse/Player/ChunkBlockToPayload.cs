using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Server.Protocol.PacketResponse.MessagePack;
using Server.Util;

namespace Server.Protocol.PacketResponse.Player
{
    public static class ChunkBlockToPayload
    {
        public static ChunkDataResponseMessagePack Convert(Coordinate chunkCoordinate,IWorldBlockDatastore worldBlockDatastore, WorldMapTile worldMapTile)
        {
            
            //ブロックのIDの取得
            var blocksIds = CoordinateToChunkBlockIntArray.GetBlockIdsInChunk(chunkCoordinate, worldBlockDatastore);
            var blockDirections = CoordinateToChunkBlockIntArray.GetBlockDirectionInChunk(chunkCoordinate, worldBlockDatastore);
            
            //マップタイルのIDの取得
            var mapTIleIds = CoordinateToChunkBlockIntArray.GetMapIdsInChunk(chunkCoordinate, worldMapTile);


            return new ChunkDataResponseMessagePack(
                chunkCoordinate,blocksIds,blockDirections,mapTIleIds);
        }
    }
}