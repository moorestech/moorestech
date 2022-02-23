using System.Collections.Generic;
using Core.Const;
using Game.World.Interface.DataStore;
using Server.Util;

namespace Server.Protocol.PacketResponse.Player
{
    public static class ChunkBlockToPayload
    {
        public static byte[] Convert(IWorldBlockDatastore _worldBlockDatastore, Coordinate chunkCoordinate)
        {
            var blocks = CoordinateToChunkBlockIntArray.Convert(chunkCoordinate, _worldBlockDatastore);
            
            var payload = new List<bool>();

            payload.AddRange(ToBitList.Convert(ToByteList.Convert((short) 1)));
            payload.AddRange(ToBitList.Convert(ToByteList.Convert(chunkCoordinate.X)));
            payload.AddRange(ToBitList.Convert(ToByteList.Convert(chunkCoordinate.Y)));
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                for (int j = 0; j < blocks.GetLength(1); j++)
                {
                    var id = blocks[i, j];
                    //空気ブロックの追加
                    if (id == BlockConst.EmptyBlockId)
                    {
                        payload.Add(false);
                        continue;
                    }

                    payload.Add(true);
                    //byte整数
                    if (byte.MinValue <= id && id <= byte.MaxValue)
                    {
                        payload.Add(false);
                        payload.Add(false);
                        payload.AddRange(ToBitList.Convert((byte) id));
                        continue;
                    }

                    //short整数
                    if (short.MinValue <= id && id <= short.MaxValue)
                    {
                        payload.Add(false);
                        payload.Add(true);
                        payload.AddRange(ToBitList.Convert(ToByteList.Convert((short) id)));
                        continue;
                    }

                    //int整数
                    payload.Add(true);
                    payload.Add(false);
                    payload.AddRange(ToBitList.Convert(ToByteList.Convert(id)));
                }
            }

            return BitListToByteList.Convert(payload).ToArray();
        }
    }
}