using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using MessagePack;
using Server.Protocol.PacketResponse.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Player
{
    public static class ChunkBlockToPayload
    {
        public static List<byte> Convert(Vector2Int chunkCoreVector2Int, IWorldBlockDatastore worldBlockDatastore,
            WorldMapTile worldMapTile)
        {
            //ブロックのIDの取得
            var blocksIds = CoordinateToChunkBlockIntArray.GetBlockIdsInChunk(chunkCoreVector2Int, worldBlockDatastore);
            var blockDirections =
                CoordinateToChunkBlockIntArray.GetBlockDirectionInChunk(chunkCoreVector2Int, worldBlockDatastore);

            //マップタイルのIDの取得
            var mapTIleIds = CoordinateToChunkBlockIntArray.GetMapIdsInChunk(chunkCoreVector2Int, worldMapTile);


            return MessagePackSerializer.Serialize(new ChunkDataResponseMessagePack(
                chunkCoreVector2Int, blocksIds, blockDirections, mapTIleIds)).ToList();
        }
    }
}