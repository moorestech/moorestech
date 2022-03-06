using MainGame.Basic;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockPlacePreview : MonoBehaviour,IBlockPlacePreview
    {
        public void SetDirection(BlockDirection blockDirection)
        {
            gameObject.transform.rotation = BlockDirectionAngle.GetRotation(blockDirection);
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}