using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Tests.PlaceSystem.TrainCostIntegration
{
    internal sealed class RecordingPierPreview : IPlacementPreviewBlockGameObjectController
    {
        internal readonly List<bool> ColorUpdates = new();
        public bool IsActive { get; private set; }

        public void SetPreview(List<PlaceInfo> placeInfos, BlockMasterElement master)
        {
            UpdatePlaceableColors(placeInfos);
        }

        public IReadOnlyList<bool> DetectGroundOverlaps()
        {
            return new[] { false };
        }

        public void UpdatePlaceableColors(List<PlaceInfo> placeInfos)
        {
            // 可変PlaceInfoの参照ではなく描画時点の値を記録する
            // Capture the drawn value rather than retaining the mutable PlaceInfo reference
            ColorUpdates.Add(placeInfos[0].Placeable);
        }

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
        {
            previewBlock = null;
            return false;
        }
    }
}
