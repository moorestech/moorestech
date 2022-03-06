using MainGame.Basic;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockPlacePreview : MonoBehaviour,IBlockPlacePreview
    {
        public void SetPreview(Vector2Int vector2Int, BlockDirection blockDirection)
        {
            transform.rotation = BlockDirectionAngle.GetRotation(blockDirection);
            transform.position = new Vector3(vector2Int.x, 0, vector2Int.y);
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}