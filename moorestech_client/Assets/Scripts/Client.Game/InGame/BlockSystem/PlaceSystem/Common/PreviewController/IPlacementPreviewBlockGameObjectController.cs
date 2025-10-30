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
        
        public List<bool> SetPreviewAndGroundDetect(List<PreviewPlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);
        
        public void SetActive(bool active);
    }
    
    public class PreviewPlaceInfo
    {
        /// <summary>
        /// サーバーと共通のブロック設置情報
        /// Block placement information shared with the server.
        /// </summary>
        public readonly PlaceInfo PlaceInfo;
        
        /// <summary>
        /// プレビュー時専用のブロックのステート情報
        /// ブロック設置プレビュー時にプレビュー個別の処理をしたいときに、ここに情報を入れて受け渡す。
        /// <see cref="IBlockPreviewStateProcessor"/> を実装して処理を行う。
        /// Block state information exclusive to preview.
        /// When you want to perform preview-specific processing during block placement preview, put the information here and pass it along.
        /// Implement <see cref="IBlockPreviewStateProcessor"/> to process it.
        /// </summary>
        public readonly Dictionary<string, byte[]> CurrentStateDetail;
        
        public PreviewPlaceInfo(PlaceInfo placeInfo, Dictionary<string, byte[]> currentStateDetail = null)
        {
            PlaceInfo = placeInfo;
            CurrentStateDetail = currentStateDetail;
        }
    }
}