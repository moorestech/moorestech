using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Input;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;

        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController)
        {
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, previewBlockController);
        }

        public override void Enable()
        {
            _trainRailPlaceSystemService.Enable();
        }

        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged)
        {
            // ビルドメニュー選択のBlockIdでプレビュー・設置を駆動する
            // Drive preview and placement from the build-menu selected BlockId
            var blockId = target.BlockId;
            var placeInfo = _trainRailPlaceSystemService.ManualUpdate(blockId);
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp || UiPointerHitTest.IsPointerOverAnyUi()) return;

            PlaceSystemUtil.SendPlaceBlockProtocol(new List<PlaceInfo> { placeInfo });
        }
        
        public override void Disable()
        {
            _trainRailPlaceSystemService.Disable();
        }
    }
}
