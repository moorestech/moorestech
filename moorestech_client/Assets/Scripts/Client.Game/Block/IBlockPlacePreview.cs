using Game.World.Interface.DataStore;
using Constant;
using Game.Block.Config;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace MainGame.UnityView.Block
{
    public interface IBlockPlacePreview
    {
        public void SetPreview(Vector2Int blockPosition, BlockDirection blockDirection,BlockConfigData blockConfig);

        public void SetActive(bool active);
    }
}