using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Control;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Game.Construction;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly ConstructionWalletQuery _constructionWalletQuery;

        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory localPlayerInventory, ConstructionWalletQuery constructionWalletQuery)
        {
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, previewBlockController);
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
        }

        public override void Enable()
        {
            _trainRailPlaceSystemService.Enable();
        }

        protected override void ManualUpdate(BlockPlacementTarget target, bool isSelectionChanged, PlacementFeedback feedback)
        {
            // ビルドメニュー選択のBlockIdでプレビュー・設置を駆動する
            // Drive preview and placement from the build-menu selected BlockId
            var blockId = target.BlockId;
            var placeInfo = _trainRailPlaceSystemService.ManualUpdate(blockId, feedback);

            // 距離外でプレビューが無ければ何もしない（理由行はサービスが積み済み）
            // Nothing to do without a preview beyond range (the service already pushed the reason)
            if (placeInfo == null) return;

            // 建設コスト不足を可否と理由行へ反映し、最終可否で塗り直す
            // Fold the construction cost shortage into placeability and the reason lines, then repaint with the final state
            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, _constructionWalletQuery, _localPlayerInventory, feedback);
            _trainRailPlaceSystemService.UpdatePreviewColor(placeInfo);

            // 地面干渉・コスト不足で設置不可なセルは送信しない（理由行は上で積み済み）
            // Do not send a cell blocked by terrain or cost shortage (the reason lines are already pushed above)
            if (!placeInfo.Placeable) return;
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp || UiPointerHitTest.IsPointerOverAnyUi()) return;

            PlaceBlockProtocolSender.SendPlaceBlockProtocol(new List<PlaceInfo> { placeInfo });
        }
        
        public override void Disable()
        {
            _trainRailPlaceSystemService.Disable();
        }
    }
}
