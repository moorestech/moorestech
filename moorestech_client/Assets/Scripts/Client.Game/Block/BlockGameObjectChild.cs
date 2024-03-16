using UnityEngine;

namespace Client.Game.Block
{
    public class BlockGameObjectChild : MonoBehaviour
    {
        public BlockGameObject BlockGameObject { get; private set; }

        public void Init(BlockGameObject blockGameObject)
        {
            BlockGameObject = blockGameObject;
        }
    }
}