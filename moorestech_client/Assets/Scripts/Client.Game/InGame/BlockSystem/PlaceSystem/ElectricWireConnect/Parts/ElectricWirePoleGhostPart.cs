using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.Construction;
using Client.Game.InGame.UI.Inventory.Main;
using TMPro;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電柱ゴーストの位置計算・表示・地面/建設コスト判定を行う共通部。延長設置と孤立設置で共有する
    /// Shared pole-ghost logic (position, display, ground and cost checks) used by extend and isolated placement
    /// </summary>
    public class ElectricWirePoleGhostPart
    {
        private const float NameLabelFontSize = 3f;

        // 通常ブロック設置と同等の設置可能距離（前例: GearChainPoleFrameInputCollector）
        // Placeable distance equivalent to common block placement (precedent: GearChainPoleFrameInputCollector)
        private const float PlaceableMaxDistance = 100f;

        private static readonly Vector3 NameLabelOffset = new(0.5f, 1.2f, 0.5f);

        private readonly Camera _mainCamera;
        private readonly IPlacementPreviewBlockGameObjectController _previewBlockController;
        private readonly ILocalPlayerInventory _inventory;
        private readonly CommonBlockPlacePointCalculator _pointCalculator;
        private readonly TextMeshPro _nameLabel;

        public ElectricWirePoleGhostPart(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory inventory, CommonBlockPlacePointCalculator pointCalculator)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _inventory = inventory;
            _pointCalculator = pointCalculator;

            // 選択中の電柱名を表示するワールド空間ラベル（ExtendPreviewObjectのコストラベルと同じ構成）
            // World-space label for the selected pole name, built the same way as ExtendPreviewObject's cost label
            var labelObject = new GameObject("ElectricWirePoleNameLabel");
            _nameLabel = labelObject.AddComponent<TextMeshPro>();
            _nameLabel.fontSize = NameLabelFontSize;
            _nameLabel.alignment = TextAlignmentOptions.Center;
            labelObject.SetActive(false);
        }

        /// <summary>
        /// カーソル位置に選択中の電柱ゴーストを計算・表示する。失敗時はラベルを隠しfalseを返す
        /// Compute and show the selected pole's ghost at the cursor; hide the label and return false on failure
        /// </summary>
        public bool TryEvaluateGhost(ElectricWirePoleSelection selection, out ElectricWirePoleGhostEvaluation evaluation)
        {
            evaluation = default;

            if (!selection.TryGetSelectedPole(out var poleBlockId, out var poleMaster)) return Fail();

            // 建設コストを賄えるかを所持素材から判定する
            // Judge from owned materials whether the construction cost is affordable
            var canAffordPole = 1 <= ConstructionMaterialAffordability.CalculateAffordableCellCount(poleMaster.RequiredItems, _inventory);

            // 電柱の設置座標を地面レイキャストから求め、設置可能距離を超えていたらゴーストを出さない
            // Compute the pole placement position from a ground raycast and drop the ghost beyond the placeable distance
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, 0, selection.CurrentDirection, poleMaster, out var placePoint, out _)) return Fail();
            if (PlaceableMaxDistance < Vector3.Distance(_mainCamera.transform.position, placePoint)) return Fail();

            // 通常設置と同じ計算でPlaceInfo生成
            // Build the pole PlaceInfo using the same calculation as normal placement
            var placeInfos = _pointCalculator.CalculatePoint(placePoint, placePoint, selection.CurrentDirection, poleMaster);

            // 地面判定はゴーストの物理接触を読むため、判定前に有効化する（前例: GearChainPoleExtendPreviewObject.PositionGhost）
            // Ground detect reads the ghost's physics contact, so activate it before judging (precedent: GearChainPoleExtendPreviewObject.PositionGhost)
            _previewBlockController.SetActive(true);

            // 設置可否を確定する（既存ブロックとの重なり判定はCalculatePoint内で織り込み済み）
            // Finalize placeability (overlap-with-existing-block judgement is already folded in by CalculatePoint)
            var groundOverlaps = _previewBlockController.SetPreviewAndGroundDetect(placeInfos, poleMaster);
            if (groundOverlaps[0]) placeInfos[0].Placeable = false;

            ShowNameLabel();
            evaluation = new ElectricWirePoleGhostEvaluation(placeInfos, poleMaster, poleBlockId, placeInfos[0].Placeable, canAffordPole);
            return true;

            #region Internal

            bool Fail()
            {
                _nameLabel.gameObject.SetActive(false);
                return false;
            }

            // 選択中の電柱名ラベルをゴースト位置に表示しカメラへ向ける
            // Show the selected pole's name label at the ghost position, billboarded to the camera
            void ShowNameLabel()
            {
                _nameLabel.gameObject.SetActive(true);
                _nameLabel.text = poleMaster.Name;

                var labelTransform = _nameLabel.transform;
                labelTransform.position = placeInfos[0].Position + NameLabelOffset;
                labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - _mainCamera.transform.position);
            }

            #endregion
        }

        /// <summary>
        /// 電柱名ラベルの表示状態を切り替える（ゴースト非表示に落とすモードから呼ぶ）
        /// Toggle the pole name label's visibility (called when a mode falls back to hiding the ghost)
        /// </summary>
        public void SetNameLabelActive(bool active)
        {
            _nameLabel.gameObject.SetActive(active);
        }
    }
}
