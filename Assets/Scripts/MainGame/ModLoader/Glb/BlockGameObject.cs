using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId { get; private set; } = 0;
        public Vector2Int BlockPosition { get; private set; } = Vector2Int.zero;

        public void SetUp(int blockId,Vector2Int position)
        {
            BlockPosition = position;
            BlockId = blockId;
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>())
            {
                child.Init(this);
            }
        }
    }
}