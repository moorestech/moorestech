using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Input;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystem : IPlaceSystem
    {
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;

        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController)
        {
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, previewBlockController);
        }

        public void Enable()
        {
            _trainRailPlaceSystemService.Enable();
        }

        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            // ビルドメニュー選択のBlockIdでプレビュー・設置を駆動する
            // Drive preview and placement from the build-menu selected BlockId
            var blockId = context.SelectedBlockId.Value;
            var placeInfo = _trainRailPlaceSystemService.ManualUpdate(blockId);
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

            PlaceSystemUtil.SendPlaceBlockProtocol(new List<PlaceInfo> { placeInfo });
        }
        
        public void Disable()
        {
            _trainRailPlaceSystemService.Disable();
        }
    }
}