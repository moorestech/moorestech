using System;
using System.Collections.Generic;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Player
{
    public class PlayerCoordinateToResponse
    {
        private const int RequestPlayerIntervalMilliSeconds = 5000;

        private Coordinate _lastCoordinate = new() { X = int.MaxValue, Y = int.MaxValue };
        private DateTime _lastGetTime = DateTime.MinValue;

        public List<Coordinate> GetResponseChunkCoordinates(Coordinate coordinate)
        {
            
            
            //5000
            if (_lastGetTime.AddMilliseconds(RequestPlayerIntervalMilliSeconds) < DateTime.Now) _lastCoordinate = new Coordinate { X = int.MaxValue, Y = int.MaxValue };

            _lastGetTime = DateTime.Now;

            var now = GetChunkCoordinates(coordinate);
            var last = GetChunkCoordinates(_lastCoordinate);
            _lastCoordinate = coordinate;
            for (var i = now.Count - 1; i >= 0; i--)
            {
                
                if (!last.Contains(now[i])) continue;
                now.RemoveAt(i);
            }

            return now;
        }

        public static List<Coordinate> GetChunkCoordinates(Coordinate coordinate)
        {
            var chunkHalf = ChunkResponseConst.PlayerVisibleRangeChunk / 2;
            
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);

            var result = new List<Coordinate>();
            for (var i = -chunkHalf; i <= chunkHalf; i++)
            for (var j = -chunkHalf; j <= chunkHalf; j++)
                result.Add(new Coordinate(
                    x + i * ChunkResponseConst.ChunkSize,
                    y + j * ChunkResponseConst.ChunkSize));

            return result;
        }
    }
}