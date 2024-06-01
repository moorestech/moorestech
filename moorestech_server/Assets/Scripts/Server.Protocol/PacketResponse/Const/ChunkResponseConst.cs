using UnityEngine;

namespace Server.Protocol.PacketResponse.Const
{
    public static class ChunkResponseConst
    {
        public const int ChunkSize = 20;
        public const int PlayerVisibleRangeChunk = 5;


        public static Vector3Int BlockPositionToChunkOriginPosition(Vector3Int pos)
        {
            return new Vector3Int(GetChunk(pos.x), GetChunk(pos.y));
        }

        private static int GetChunk(int n)
        {
            var chunk = n / ChunkSize;


            if (n < 0 && n % ChunkSize != 0) chunk--;
            var chunkPosition = chunk * ChunkSize;
            return chunkPosition;
        }
    }
}