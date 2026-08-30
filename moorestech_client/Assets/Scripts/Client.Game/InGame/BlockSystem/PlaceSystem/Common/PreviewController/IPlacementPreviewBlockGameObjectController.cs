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
        
        /// <summary>
        /// プレビューブロックを配置する。地形との接触は見ない
        /// Places the preview blocks without looking at terrain contact
        /// </summary>
        public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);

        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);

        /// <summary>
        /// PlaceInfoのPlaceable状態に基づいてプレビューブロックの色を更新する
        /// Update preview block colors based on PlaceInfo's Placeable state
        /// </summary>
        public void UpdatePlaceableColors(List<PlaceInfo> placeInfos);

        public void SetActive(bool active);

        /// <summary>
        /// アクティブなプレビューブロックをインデックスで取り出す（SetPreviewAndGroundDetectの順序と一致）
        /// Fetch an active preview block by index, matching SetPreviewAndGroundDetect ordering
        /// </summary>
        public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock);
    }
}