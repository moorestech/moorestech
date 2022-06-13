using System;
using System.Collections.Generic;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Const;
using Server.Protocol.PacketResponse.Util;

namespace Server.Protocol.PacketResponse.Player
{
    public class PlayerCoordinateToResponse
    {
        private readonly int _playerId;
        private readonly IEntitiesDatastore _entitiesDatastore;
        
        
        private const int RequestPlayerIntervalMilliSeconds = 500;
        
        private Coordinate _lastCoordinate = new Coordinate {X = Int32.MaxValue, Y = Int32.MaxValue};
        private DateTime _lastGetTime = DateTime.MinValue;

        public PlayerCoordinateToResponse(int playerId, IEntitiesDatastore entitiesDatastore)
        {
            _playerId = playerId;
            _entitiesDatastore = entitiesDatastore;
        }

        public List<Coordinate> GetResponseChunkCoordinates()
        {
            //例えばユーザーが一度ログアウトして、再度ログインすると、クライアント側ではブロックの情報は消えているが、
            //サーバー側では前回との差分しか返さないようになってしまう
            //そのため、前回の取得から500ミリ秒以上経過している場合は、前回座標のリセットを行う
            if (_lastGetTime.AddMilliseconds(RequestPlayerIntervalMilliSeconds) < DateTime.Now)
            {
                _lastCoordinate = new Coordinate {X = Int32.MaxValue, Y = Int32.MaxValue};
            }
            _lastGetTime = DateTime.Now;
            
            

            //今の座標を取得
            var position = _entitiesDatastore.GetPosition(_playerId);
            //三次元の座標系はY up, X right, Z frontなので、Y軸にZを入れる
            var nowCoordinate = new Coordinate {X = (int)position.X, Y = (int)position.Z};

            
            
            var now = GetCoordinates(nowCoordinate);
            var last = GetCoordinates(_lastCoordinate);
            _lastCoordinate = nowCoordinate;
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
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coordinate.X, coordinate.Y);

            var result = new List<Coordinate>();
            for (int i = -chunkHalf; i <= chunkHalf; i++)
            {
                for (int j = -chunkHalf; j <= chunkHalf; j++)
                {
                    result.Add(new Coordinate(
                        x + i * ChunkResponseConst.ChunkSize,
                        y + j * ChunkResponseConst.ChunkSize));
                }
            }

            return result;
        }
    }
}