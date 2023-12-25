using UnityEngine;

namespace Constant
{
    public static class ChunkConstant
    {
        public const int ChunkSize = 20;

        public static Vector2Int BlockPositionToChunkOriginPosition(Vector2Int blockPosition)
        {
            return new Vector2Int(GetChunk(blockPosition.x), GetChunk(blockPosition.y));
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