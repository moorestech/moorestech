using System;
using System.Collections.Generic;
using Core.Util;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Const;

namespace Server.Protocol.PacketResponse.Player
{
    public class PlayerCoordinateToResponse
    {
        private const int RequestPlayerIntervalMilliSeconds = 5000;

        private CoreVector2Int _lastCoreVector2Int = new() { X = int.MaxValue, Y = int.MaxValue };
        private DateTime _lastGetTime = DateTime.MinValue;

        public List<CoreVector2Int> GetResponseChunkCoordinates(CoreVector2Int coreVector2Int)
        {
            //例えばユーザーが一度ログアウトして、再度ログインすると、クライアント側ではブロックの情報は消えているが、
            //サーバー側では前回との差分しか返さないようになってしまう
            //そのため、前回の取得から5000ミリ秒以上経過している場合は、前回座標のリセットを行う
            if (_lastGetTime.AddMilliseconds(RequestPlayerIntervalMilliSeconds) < DateTime.Now) _lastCoreVector2Int = new CoreVector2Int { X = int.MaxValue, Y = int.MaxValue };

            _lastGetTime = DateTime.Now;

            var now = GetChunkCoordinates(coreVector2Int);
            var last = GetChunkCoordinates(_lastCoreVector2Int);
            _lastCoreVector2Int = coreVector2Int;
            for (var i = now.Count - 1; i >= 0; i--)
            {
                //もし前回の取得チャンクに今回の取得チャンクとの被りがあったら削除する
                if (!last.Contains(now[i])) continue;
                now.RemoveAt(i);
            }

            return now;
        }

        public static List<CoreVector2Int> GetChunkCoordinates(CoreVector2Int coreVector2Int)
        {
            var chunkHalf = ChunkResponseConst.PlayerVisibleRangeChunk / 2;
            //その座標のチャンクの原点
            var (x, y) = ChunkResponseConst.BlockPositionToChunkOriginPosition(coreVector2Int.X, coreVector2Int.Y);

            var result = new List<CoreVector2Int>();
            for (var i = -chunkHalf; i <= chunkHalf; i++)
            for (var j = -chunkHalf; j <= chunkHalf; j++)
                result.Add(new CoreVector2Int(
                    x + i * ChunkResponseConst.ChunkSize,
                    y + j * ChunkResponseConst.ChunkSize));

            return result;
        }
    }
}