using Game.World.Interface.DataStore;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        public void SetPreview(Vector2Int vector2Int, BlockDirection blockDirection)
        {
            //0.5のオフセットをすることで正しい位置に設定する
            var (position, rotation, scale) = SlopeBlockPlaceSystem.GetSlopeBeltConveyorTransform(new Vector3(vector2Int.x, 0, vector2Int.y).AddBlockPlaceOffset(), blockDirection);
            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = scale;
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}