using MainGame.Basic;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockPlacePreview : MonoBehaviour,IBlockPlacePreview
    {
        public void SetPreview(Vector2Int vector2Int, BlockDirection blockDirection)
        {
            transform.rotation = BlockDirectionAngle.GetRotation(blockDirection);
            //0.5のオフセットをすることで正しい位置に設定する
            transform.position = new Vector3(vector2Int.x + 0.5f, 0, vector2Int.y + 0.5f);
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}