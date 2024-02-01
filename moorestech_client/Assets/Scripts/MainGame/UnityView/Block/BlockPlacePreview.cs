using Game.World.Interface.DataStore;
using Constant;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public class BlockPlacePreview : MonoBehaviour, IBlockPlacePreview
    {
        public void SetPreview(Vector2Int vector2Int, BlockDirection blockDirection,int blockId)
        {
            //TODO ブロックのプレビューを表示する
            
            //0.5のオフセットをすることで正しい位置に設定する
            var (position, rotation, scale) = SlopeBlockPlaceSystem.GetSlopeBeltConveyorTransform(vector2Int, blockDirection,Vector2Int.one);
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