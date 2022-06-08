using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Server.Util;

namespace Server.Protocol.PacketResponse.Player
{
    public static class ChunkBlockToPayload
    {
        public static List<byte> Convert(Coordinate chunkCoordinate,IWorldBlockDatastore worldBlockDatastore, WorldMapTile worldMapTile)
        {
            
            var payload = new List<bool>();

            payload.AddRange(ToBitList.Convert(ToByteList.Convert((short) 1)));
            payload.AddRange(ToBitList.Convert(ToByteList.Convert(chunkCoordinate.X)));
            payload.AddRange(ToBitList.Convert(ToByteList.Convert(chunkCoordinate.Y)));
            
            //ブロックのIDの取得
            var blocksIds = CoordinateToChunkBlockIntArray.GetBlockIdsInChunk(chunkCoordinate, worldBlockDatastore);
            var blockDirections = CoordinateToChunkBlockIntArray.GetBlockDirectionInChunk(chunkCoordinate, worldBlockDatastore);
            
            //マップタイルのIDの取得
            var mapTIleIds = CoordinateToChunkBlockIntArray.GetMapIdsInChunk(chunkCoordinate, worldMapTile);


            return MessagePackSerializer.Serialize(new ChunkDataResponseMessagePack(
                chunkCoordinate,blocksIds,blockDirections,mapTIleIds)).ToList();
        }
    }
}