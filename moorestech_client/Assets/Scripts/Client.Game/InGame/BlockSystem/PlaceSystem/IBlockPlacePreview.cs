using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IBlockPlacePreview
    {
        bool IsActive { get; }
        
        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);
        
        public void SetActive(bool active);
    }
}