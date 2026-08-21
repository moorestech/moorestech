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
        // 通常ブロック設置と同等の設置可能距離（前例: GearChainPoleFrameInputCollector）
        // Placeable distance equivalent to common block placement (precedent: GearChainPoleFrameInputCollector)
        private const float PlaceableMaxDistance = 100f;

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
        /// カーソル位置に選択中の電柱ゴーストを計算・表示する。ゴーストを出せないときは理由だけ積みfalseを返す
        /// Compute and show the selected pole's ghost at the cursor; on failure push only the reason and return false
        /// </summary>
        public bool TryEvaluateGhost(ElectricWirePoleSelection selection, PlacementFeedback feedback, out ElectricWirePoleGhostEvaluation evaluation)
        {
            evaluation = default;

            if (!selection.TryGetSelectedPole(out var poleBlockId, out var poleMaster)) return false;

            // 電柱1本分の建設コスト不足を所持素材から求める（空なら賄える）
            // Compute the construction shortages for one pole from owned materials (empty means affordable)
            var materialShortages = ConstructionCostShortageCalculator.Calculate(poleMaster.RequiredItems, 1, _inventory);

            // 電柱の設置座標を地面レイキャストから求める。距離超過は理由だけ出してゴーストは出さない
            // Compute the pole position from a ground raycast; beyond the placeable distance show only the reason and no ghost
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, 0, selection.CurrentDirection, poleMaster, out var placePoint, out _)) return false;
            if (PlaceableMaxDistance < Vector3.Distance(_mainCamera.transform.position, placePoint))
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
