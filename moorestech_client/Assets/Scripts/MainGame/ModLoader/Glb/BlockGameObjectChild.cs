using UnityEngine;

namespace MainGame.ModLoader.Glb
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