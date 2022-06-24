using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId { get; private set; } = 0;

        public void SetUp(int blockId)
        {
            BlockId = blockId;
            foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>())
            {
                child.Init(this);
            }
        }
    }
}