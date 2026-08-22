using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電柱ゴーストの位置計算・表示・地面/建設コスト判定を行う共通部。延長設置と孤立設置で共有する
    /// Shared pole-ghost logic (position, display, ground and cost checks) used by extend and isolated placement
    /// </summary>
    public class ElectricWirePoleGhostPart
    {
        private readonly Camera _mainCamera;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _inventory;
        private readonly CommonBlockPlacePointCalculator _pointCalculator;

        public ElectricWirePoleGhostPart(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory inventory, CommonBlockPlacePointCalculator pointCalculator)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _inventory = inventory;
            _pointCalculator = pointCalculator;
        }

        /// <summary>
        /// 電柱ゴーストを計算表示。出せない場合は理由のみ積みfalse
        /// Computes and shows the pole ghost; on failure pushes only the reason and returns false
        /// </summary>
        public bool TryEvaluateGhost(ElectricWirePoleSelection selection, PlacementFeedback feedback, out ElectricWirePoleGhostEvaluation evaluation)
        {
            evaluation = default;

            if (!selection.TryGetSelectedPole(out var poleBlockId, out var poleMaster)) return false;

            // 電柱1本分のコスト不足を所持素材から算出
            // Computes one pole's cost shortage from owned materials
            var materialShortages = ConstructionCostShortageCalculator.Calculate(poleMaster.RequiredItems, 1, _inventory);

            // 地面レイキャストで座標算出。距離超過は理由のみ出す
            // Computes the position via ground raycast; beyond range shows only the reason
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, 0, selection.CurrentDirection, poleMaster, out var placePoint, out _)) return false;
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(placePoint, PlaceSystemUtil.PlaceableMaxDistance))
            {
                feedback.AddTooFar();
                return false;
            }

            // 通常設置と同じ計算でPlaceInfo生成。この時点のPlaceable=falseは既存ブロック重複
            // Build the pole PlaceInfo like normal placement; Placeable=false here means existing-block overlap
            var placeInfos = _pointCalculator.CalculatePoint(placePoint, placePoint, selection.CurrentDirection, poleMaster);
            var isPositionFree = placeInfos[0].Placeable;

            // 地面判定はゴーストの物理接触を読むため、判定前に有効化する（前例: GearChainPoleExtendPreviewObject.PositionGhost）
            // Ground detect reads the ghost's physics contact, so activate it before judging (precedent: GearChainPoleExtendPreviewObject.PositionGhost)
            _previewBlockController.SetActive(true);

            var groundOverlaps = _previewBlockController.SetPreviewAndGroundDetect(placeInfos, poleMaster);
            var isGroundClear = !groundOverlaps[0];
            if (!isGroundClear) placeInfos[0].Placeable = false;

            evaluation = new ElectricWirePoleGhostEvaluation(placeInfos, poleMaster, poleBlockId, isGroundClear, isPositionFree, materialShortages);
            return true;
        }
    }
}
