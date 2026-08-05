using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;
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
        private static readonly Vector3 NameLabelOffset = new(0.5f, 1.2f, 0.5f);

        private readonly ElectricWireToolContext _context;
        private readonly CommonBlockPlacePointCalculator _pointCalculator;
        private readonly TextMeshPro _nameLabel;

        public ElectricWirePoleGhostPart(ElectricWireToolContext context, CommonBlockPlacePointCalculator pointCalculator)
        {
            _context = context;
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
            var canAffordPole = ConstructionCostPreviewCalculator.CalculateAffordableCellCount(poleMaster.RequiredItems, _context.Inventory) >= 1;

            // 電柱の設置座標を地面レイキャストから求める
            // Compute the pole placement position from a ground raycast
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_context.MainCamera, 0, selection.CurrentDirection, poleMaster, out var placePoint, out _)) return Fail();

            if (poleMaster.BlockParam is not ElectricPoleBlockParam poleParam) return Fail();

            // 通常設置と同じ計算でPlaceInfo生成
            // Build the pole PlaceInfo using the same calculation as normal placement
            var placeInfos = _pointCalculator.CalculatePoint(placePoint, placePoint, selection.CurrentDirection, poleMaster);
            var placeInfo = placeInfos[0];

            // 設置可否を確定する（既存ブロックとの重なり判定はCalculatePoint内で織り込み済み）
            // Finalize placeability (overlap-with-existing-block judgement is already folded in by CalculatePoint)
            var groundOverlaps = _context.PreviewBlockController.SetPreviewAndGroundDetect(placeInfos, poleMaster);
            if (groundOverlaps[0]) placeInfo.Placeable = false;
            var groundClear = placeInfo.Placeable;

            ShowNameLabel(placeInfo, poleMaster);
            evaluation = new ElectricWirePoleGhostEvaluation(placeInfos, placeInfo, poleMaster, poleBlockId, poleParam, groundClear, canAffordPole);
            return true;

            #region Internal

            bool Fail()
            {
                _nameLabel.gameObject.SetActive(false);
                return false;
            }

            #endregion
        }

        /// <summary>
        /// 名前ラベルの表示状態を切り替える（ゴースト非表示に落とすモードから呼ぶ）
        /// Toggle the name label's visibility (called when a mode falls back to hiding the ghost)
        /// </summary>
        public void SetActive(bool active)
        {
            _nameLabel.gameObject.SetActive(active);
        }

        // 選択中の電柱名ラベルをゴースト位置に表示しカメラへ向ける
        // Show the selected pole's name label at the ghost position, billboarded to the camera
        private void ShowNameLabel(PlaceInfo placeInfo, BlockMasterElement poleMaster)
        {
            _nameLabel.gameObject.SetActive(true);
            _nameLabel.text = poleMaster.Name;

            var labelTransform = _nameLabel.transform;
            labelTransform.position = placeInfo.Position + NameLabelOffset;
            labelTransform.rotation = Quaternion.LookRotation(labelTransform.position - _context.MainCamera.transform.position);
        }
    }
}
