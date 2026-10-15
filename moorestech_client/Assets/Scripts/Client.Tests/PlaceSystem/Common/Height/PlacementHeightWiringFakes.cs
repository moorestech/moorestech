using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Tests.PlaceSystem.Common.Height
{
    // 高さの配線テストで入力と描画の依存を置き換える
    // Replaces selection and rendering dependencies in height wiring tests
    internal static class PlacementHeightWiringFakes
    {
        // 設置系を1度だけ高さ非対応の本番EmptyPlaceSystemへ切り替えるセレクタ
        // A selector that switches once from the given system to the production height-less EmptyPlaceSystem
        internal sealed class SwitchableSelector : IPlaceSystemSelector
        {
            public IPlaceSystem EmptyPlaceSystem { get; } = new EmptyPlaceSystem();

            private IPlaceSystem _current;

            public SwitchableSelector(IPlaceSystem current)
            {
                _current = current;
            }

            public void SwitchToEmpty()
            {
                _current = EmptyPlaceSystem;
            }

            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => _current;
        }

        // Disable()内のSetActive(false)以外は呼ばれない前提のno-opフェイク
        // A no-op fake; only SetActive(false) inside Disable() is expected to run
        internal sealed class NullPreviewController : IPlacementPreviewBlockGameObjectController
        {
            public bool IsActive { get; private set; }
            public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster) { }

            public void UpdatePlaceableColors(List<PlaceInfo> placeInfos) { }
            public void SetActive(bool active) => IsActive = active;

            public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
            {
                previewBlock = null;
                return false;
            }
        }

        internal sealed class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}
