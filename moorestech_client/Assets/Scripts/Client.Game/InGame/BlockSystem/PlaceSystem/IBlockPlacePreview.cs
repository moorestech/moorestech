using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IBlockPlacePreview
    {
        bool IsActive { get; }
        
        bool IsCollisionGround { get; }
        
        public void SetPreview(bool placeable, List<PlaceInfo> currentPlaceInfos, BlockConfigData holdingBlockConfig);
        
        public void SetActive(bool active);
    }
}