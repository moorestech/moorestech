using System;
using System.Collections.Generic;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Player
{
    public class PlayerCoordinateToResponse
    {
        private Coordinate _lastCoordinate = new Coordinate {X = Int32.MaxValue, Y = Int32.MaxValue};
        public List<Coordinate> GetResponseCoordinate(Coordinate coordinate)
        {
            var now = GetCoordinates(coordinate);
            var last = GetCoordinates(_lastCoordinate);
            _lastCoordinate = coordinate;
            for (int i = now.Count - 1; i >= 0; i--)
            {
                //もし前回の取得チャンクに今回の取得チャンクとの被りがあったら削除する
                if (!last.Contains(now[i])) continue;
                now.RemoveAt(i);
            }

            return now;
        }

        private List<Coordinate> GetCoordinates(Coordinate coordinate)
        {
            var chunkHalf = ChunkResponseConst.PlayerVisibleRangeChunk / 2;
            //その座標のチャンクの原点
            var x = coordinate.X / ChunkResponseConst.ChunkSize * ChunkResponseConst.ChunkSize;
            var y = coordinate.Y / ChunkResponseConst.ChunkSize * ChunkResponseConst.ChunkSize;
            
            var result = new List<Coordinate>();
            for (int i = -chunkHalf; i <= chunkHalf; i++)
            {
                for (int j = -chunkHalf; j <= chunkHalf; j++)
                {
                    result.Add(CoordinateCreator.New(
                        x + i * ChunkResponseConst.ChunkSize,
                        y + j * ChunkResponseConst.ChunkSize));
                }
            }

            return result;
        }
    }
}