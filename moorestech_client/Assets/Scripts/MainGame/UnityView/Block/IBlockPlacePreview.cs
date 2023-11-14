using Game.World.Interface.DataStore;
using MainGame.Basic;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public interface IBlockPlacePreview
    {
        public void SetPreview(Vector2Int vector2Int, BlockDirection blockDirection);

        public void SetActive(bool active);
    }
}