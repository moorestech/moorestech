using System;
using System.Collections.Generic;
using industrialization.Core;
using industrialization.OverallManagement.DataStore;
using industrialization.OverallManagement.Util;
using industrialization.Server.Const;

namespace industrialization.Server.Player
{
    public class PlayerCoordinateToResponse
    {
        private Coordinate _lastCoordinate = new Coordinate {x = Int32.MaxValue, y = Int32.MaxValue};
        public List<Coordinate> GetResponseCoordinate(Coordinate coordinate)
        {
            //TODO ここに座標取得処理を作成
            var now = GetCoordinates(coordinate);
            var last = GetCoordinates(_lastCoordinate);
            _lastCoordinate = coordinate;
            for (int i = now.Count - 1; i >= 0; i--)
            {
                for (int j = last.Count - 1; j >= 0; j--)
                {
                    //もし前回の取得チャンクに今回の取得チャンクとの被りがあったら削除する
                    if (!now.Contains(last[j])) continue;
                    now.RemoveAt(i);
                    last.RemoveAt(j);
                    break;
                }
            }

            return now;
        }

        private List<Coordinate> GetCoordinates(Coordinate coordinate)
        {
            var chunkHalf = ChunkResponseConst.PlayerVisibleRangeChunk / 2;
            //その座標のチャンクの原点
            var x = coordinate.x / ChunkResponseConst.ChunkSize * ChunkResponseConst.ChunkSize;
            var y = coordinate.y / ChunkResponseConst.ChunkSize * ChunkResponseConst.ChunkSize;
            
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