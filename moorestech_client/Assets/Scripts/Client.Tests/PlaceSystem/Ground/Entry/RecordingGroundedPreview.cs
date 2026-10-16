using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Tests.PlaceSystem.Ground.Entry
{
    public sealed class RecordingGroundedPreview : IPlacementPreviewBlockGameObjectController
    {
        public bool IsActive { get; private set; }
        public readonly List<PlaceInfo> Cells = new();

        public void SetPreview(List<PlaceInfo> placeInfos, BlockMasterElement master)
        {
            // 更新後の内部リスト交換に影響されず入口の出力を残す
            // Preserve entry output independently of later internal list replacements
            Cells.Clear();
            Cells.AddRange(placeInfos);
        }

        public void UpdatePlaceableColors(List<PlaceInfo> placeInfos) { }
        public void SetActive(bool active) => IsActive = active;

        public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
        {
            previewBlock = null;
            return false;
        }
    }
}
