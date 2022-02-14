using UnityEngine;

namespace MainGame.UnityView.Chunk
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