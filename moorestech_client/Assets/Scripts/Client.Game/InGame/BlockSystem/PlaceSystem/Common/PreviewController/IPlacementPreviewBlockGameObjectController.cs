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
        /// プレビューブロックを配置する。地形を設置不可の理由にするかは呼び出し側が決める
        /// Places the preview blocks; whether terrain blocks placement is the caller's decision
        /// </summary>
        public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);

        /// <summary>
        /// 直近の物理ステップ時点の地形接触を、直前のSetPreviewの並び順で返す
        /// Returns the terrain contact as of the last physics step, ordered by the preceding SetPreview
        /// </summary>
        public IReadOnlyList<bool> DetectGroundOverlaps();

        /// <summary>
        /// PlaceInfoのPlaceable状態に基づいてプレビューブロックの色を更新する
        /// Update preview block colors based on PlaceInfo's Placeable state
        /// </summary>
        public void UpdatePlaceableColors(List<PlaceInfo> placeInfos);

        public void SetActive(bool active);

        /// <summary>
        /// 直前のSetPreviewの並び順と一致
        /// Matches the ordering of the preceding SetPreview call
        /// </summary>
        public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock);
    }
}