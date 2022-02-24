using System.Collections.Generic;
using Core.Const;
using Game.World.Interface.DataStore;
using Game.WorldMap;
using Server.Util;

namespace Server.Protocol.PacketResponse.Player
{
    public static class ChunkBlockToPayload
    {
        public static byte[] Convert(Coordinate chunkCoordinate,IWorldBlockDatastore worldBlockDatastore, WorldMapTile worldMapTile)
        {
            
            var payload = new List<bool>();

            payload.AddRange(ToBitList.Convert(ToByteList.Convert((short) 1)));
            payload.AddRange(ToBitList.Convert(ToByteList.Convert(chunkCoordinate.X)));
            payload.AddRange(ToBitList.Convert(ToByteList.Convert(chunkCoordinate.Y)));
            
            //ブロックのIDの追加
            var blocks = CoordinateToChunkBlockIntArray.GetBlockIdsInChunk(chunkCoordinate, worldBlockDatastore);
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                for (int j = 0; j < blocks.GetLength(1); j++)
                {
                    var blockId = blocks[i, j];
                    SetBlockId(payload, blockId);
                }
            }
            //マップタイルのIDの追加
            var mapTIleIds = CoordinateToChunkBlockIntArray.GetMapIdsInChunk(chunkCoordinate, worldMapTile);
            for (int i = 0; i < mapTIleIds.GetLength(0); i++)
            {
                for (int j = 0; j < mapTIleIds.GetLength(1); j++)
                {
                    var mapTile = mapTIleIds[i, j];
                    SetBlockId(payload, mapTile);
                }
            }
            
            
            return BitListToByteList.Convert(payload).ToArray();
        }

        private static void SetBlockId(List<bool> payload,int id)
        {
            //空気ブロックの追加
            if (id == BlockConst.EmptyBlockId)
            {
                payload.Add(false);
                return;
            }

            payload.Add(true);
            //byte整数
            if (byte.MinValue <= id && id <= byte.MaxValue)
            {
                payload.Add(false);
                payload.Add(false);
                payload.AddRange(ToBitList.Convert((byte) id));
                return;
            }

            //short整数
            if (short.MinValue <= id && id <= short.MaxValue)
            {
                payload.Add(false);
                payload.Add(true);
                payload.AddRange(ToBitList.Convert(ToByteList.Convert((short) id)));
                return;
            }

            //int整数
            payload.Add(true);
            payload.Add(false);
            payload.AddRange(ToBitList.Convert(ToByteList.Convert(id)));
        }
    }
}