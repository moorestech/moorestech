using System.Collections.Generic;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController
{
    /// <summary>
    /// ブロックを設置する時に、設置中に表示するプレビューブロックの、実態となるGameObjectを管理するコントローラーのインターフェース
    /// Interface for the controller that manages the actual GameObject of the preview block displayed during block placement
    /// </summary>
    public interface IPlacementPreviewBlockGameObjectController
    {
        bool IsActive { get; }
        
        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);
        
        public void SetActive(bool active);
    }
}