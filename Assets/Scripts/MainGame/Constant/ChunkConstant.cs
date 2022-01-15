using UnityEngine;

namespace MainGame.Constant
{
    public static class ChunkConstant
    {
        public const int ChunkSize = 20;
        
        public static Vector2Int BlockPositionToChunkOriginPosition(Vector2Int blockPosition)
        {
            var x = blockPosition.x / ChunkConstant.ChunkSize * ChunkConstant.ChunkSize;
            var y = blockPosition.y / ChunkConstant.ChunkSize * ChunkConstant.ChunkSize;
            return new Vector2Int(x, y);
        }
    }
}