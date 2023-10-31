using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId { get; private set; } = 0;
        public Vector2Int BlockPosition { get; private set; } = Vector2Int.zero;
        public IBlockStateChangeProcessor BlockStateChangeProcessor { get; private set; }

        public void Initialize(int blockId,Vector2Int position,IBlockStateChangeProcessor blockStateChangeProcessor)
        {
            BlockPosition = position;
            BlockId = blockId;
            BlockStateChangeProcessor = blockStateChangeProcessor;
            
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>())
            {
                child.Init(this);
            }
        }
    }
}