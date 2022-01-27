using UnityEngine;

namespace MainGame.UnityView.Interface
{
    public interface IPlaceBlockGameObject
    {
        public void OnChunkUpdate(Vector2Int chunkOriginPosition,int[,] blocks);
        public void OnBlockUpdate(Vector2Int blockPosition,int blockId);
    }
}