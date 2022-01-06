using System.Collections.Generic;
using Core.Block;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using Server.Util;

namespace Server.PacketHandle.PacketResponse.Player
{
    public static class ChunkBlockToPayload
    {
        public static byte[] Convert(int[,] blocks, Coordinate chunkCoordinate)
        {
            var payload = new List<bool>();

            payload.AddRange(ByteListToBitList.Convert(ByteListConverter.ToByteArray((short) 1)));
            payload.AddRange(ByteListToBitList.Convert(ByteListConverter.ToByteArray(chunkCoordinate.X)));
            payload.AddRange(ByteListToBitList.Convert(ByteListConverter.ToByteArray(chunkCoordinate.Y)));
            for (int i = 0; i < blocks.GetLength(0); i++)
            {
                for (int j = 0; j < blocks.GetLength(1); j++)
                {
                    var id = blocks[i, j];
                    //空気ブロックの追加
                    if (id == BlockConst.BlockConst.NullBlockId)
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
                        payload.AddRange(ByteListToBitList.Convert((byte) id));
                        continue;
                    }

                    //short整数
                    if (short.MinValue <= id && id <= short.MaxValue)
                    {
                        payload.Add(false);
                        payload.Add(true);
                        payload.AddRange(ByteListToBitList.Convert(ByteListConverter.ToByteArray((short) id)));
                        continue;
                    }

                    //int整数
                    payload.Add(true);
                    payload.Add(false);
                    payload.AddRange(ByteListToBitList.Convert(ByteListConverter.ToByteArray(id)));
                }
            }

            return BitListToByteList.Convert(payload).ToArray();
        }
    }
}