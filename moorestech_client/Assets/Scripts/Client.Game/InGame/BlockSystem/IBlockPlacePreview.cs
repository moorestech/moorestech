using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem
{
    public interface IBlockPlacePreview
    {
        bool IsActive { get; }

        public void SetPreview(Vector3Int blockPosition, BlockDirection blockDirection, BlockConfigData blockConfig);

        public void SetActive(bool active);
    }
}