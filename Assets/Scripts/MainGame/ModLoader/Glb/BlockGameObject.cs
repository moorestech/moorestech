using UnityEngine;

namespace MainGame.ModLoader.Glb
{
    public class BlockGameObject : MonoBehaviour
    {
        public int BlockId => _blockId;
        private int _blockId = 0;
        public void Construct(int blockId)
        {
            _blockId = blockId;
        }
    }
}