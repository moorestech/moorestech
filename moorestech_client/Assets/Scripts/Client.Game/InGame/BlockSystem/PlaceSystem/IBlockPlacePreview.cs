using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IBlockPlacePreview
    {
        bool IsActive { get; }
        
        bool IsCollisionGround { get; }
        
        public void SetPreview(bool placeable, Vector3Int startPoint, Vector3Int endPoint, bool isStartZDirection, BlockDirection blockDirection, BlockConfigData blockConfig);
        
        public void SetActive(bool active);
    }
}