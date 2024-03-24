using System;
using System.Collections.Generic;
using Server.Protocol.PacketResponse.Const;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Player
{
    public class PlayerCoordinateToResponse
    {
        private const int RequestPlayerIntervalMilliSeconds = 5000;

        private Vector3Int _lastCoreVector2Int = new(int.MaxValue, int.MaxValue);
        private DateTime _lastGetTime = DateTime.MinValue;

        public List<Vector3Int> GetResponseChunkCoordinates(Vector3Int coreVector2Int)
        {
            //例えばユーザーが一度ログアウトして、再度ログインすると、クライアント側ではブロックの情報は消えているが、
            //サーバー側では前回との差分しか返さないようになってしまう
            //そのため、前回の取得から5000ミリ秒以上経過している場合は、前回座標のリセットを行う
            if (_lastGetTime.AddMilliseconds(RequestPlayerIntervalMilliSeconds) < DateTime.Now)
                _lastCoreVector2Int = new Vector3Int(int.MaxValue, int.MaxValue);

            _lastGetTime = DateTime.Now;

            List<Vector3Int> now = GetChunkCoordinates(coreVector2Int);
            List<Vector3Int> last = GetChunkCoordinates(_lastCoreVector2Int);
            _lastCoreVector2Int = coreVector2Int;
            for (var i = now.Count - 1; i >= 0; i--)
            {
                //もし前回の取得チャンクに今回の取得チャンクとの被りがあったら削除する
                if (!last.Contains(now[i])) continue;
                now.RemoveAt(i);
            }

            return now;
        }

        public static List<Vector3Int> GetChunkCoordinates(Vector3Int blockPos)
        {
            var chunkHalf = ChunkResponseConst.PlayerVisibleRangeChunk / 2;
            //その座標のチャンクの原点
            var chunkPos = ChunkResponseConst.BlockPositionToChunkOriginPosition(blockPos);

            var result = new List<Vector3Int>();
            for (var i = -chunkHalf; i <= chunkHalf; i++)
            for (var j = -chunkHalf; j <= chunkHalf; j++)
                result.Add(new Vector3Int(
                    chunkPos.x + i * ChunkResponseConst.ChunkSize,
                    chunkPos.y + j * ChunkResponseConst.ChunkSize));

            return result;
        }
    }
}