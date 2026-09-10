using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Game.Construction;
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
        private readonly ConstructionWalletQuery _walletQuery;

        public ElectricWirePoleGhostPart(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory inventory, CommonBlockPlacePointCalculator pointCalculator, ConstructionWalletQuery walletQuery)
        {
            _mainCamera = mainCamera;
            _previewBlockController = previewBlockController;
            _inventory = inventory;
            _pointCalculator = pointCalculator;
            _walletQuery = walletQuery;
        }

        /// <summary>
        /// 電柱ゴーストを計算表示し、可否に関わらず不可理由をfeedbackへ積む
        /// Computes and shows the pole ghost, pushing block reasons into the feedback either way
        /// </summary>
        public bool TryEvaluateGhost(ElectricWirePoleSelection selection, PlacementFeedback feedback, out ElectricWirePoleGhostEvaluation evaluation)
        {
            evaluation = default;

            if (!selection.TryGetSelectedPole(out var poleBlockId, out var poleMaster)) return false;

            // 電柱1本分の不足素材。財布の残りで賄えるなら必要セット数0となり不足は出ない
            // One pole's material shortage; a wallet-covered pole needs zero cost sets and shows no shortage
            var requiredCostSets = _walletQuery.GetRequiredCostSets(poleBlockId, 1);
            var materialShortages = ConstructionCostShortageCalculator.Calculate(poleMaster.RequiredItems, requiredCostSets, _inventory);

            // 地面レイキャストで座標算出。距離超過は理由のみ出す
            // Computes the position via ground raycast; beyond range shows only the reason
            if (!PlaceSystemUtil.TryGetRayHitBlockPosition(_mainCamera, 0, selection.CurrentDirection, poleMaster, out var placePoint, out _)) return false;
            if (!PlaceSystemUtil.IsPlaceableFromPlayer(placePoint))
            {
                feedback.AddTooFar();
                return false;
            }

            // 電線判定へ渡す予約分。サーバーがPlaceBlockProtocolで plan.ItemsToConsume を押さえるのと同じ形
            // The reservation handed to the wire judgement, the same shape the server claims as plan.ItemsToConsume
            var poleConstructionItemCounts = _walletQuery.GetItemsToConsume(poleBlockId);

            // 通常設置と同じ計算でPlaceInfo生成。この時点のPlaceable=falseは既存ブロック重複
            // Build the pole PlaceInfo like normal placement; Placeable=false here means existing-block overlap
            var run = CommonBlockPlacePointCalculator.CalculateRun(placePoint, placePoint, selection.CurrentDirection, poleMaster);
            _pointCalculator.EvaluateExistingBlockCauses(run);
            var placeInfos = run.Cells;
            var isPositionFree = placeInfos[0].Placeable;

            // 地面判定はゴーストの物理接触を読むため、判定前に有効化する（前例: GearChainPoleExtendPreviewObject.PositionGhost）
            // Ground detect reads the ghost's physics contact, so activate it before judging (precedent: GearChainPoleExtendPreviewObject.PositionGhost)
            _previewBlockController.SetActive(true);

            _previewBlockController.SetPreview(placeInfos, poleMaster);
            var groundOverlaps = _previewBlockController.DetectGroundOverlaps();

            // 接触は可否として返すだけ。塗りとPlaceableの確定はモード側が持つ
            // Contact is only returned as a judgement; painting and Placeable stay with the mode
            var isGroundClear = !groundOverlaps[0];

            evaluation = new ElectricWirePoleGhostEvaluation(placeInfos, poleMaster, poleBlockId, isGroundClear, isPositionFree, materialShortages, poleConstructionItemCounts);

            // ゴーストを出せた時点でその不可理由を積む。呼び出し元は表示と送信だけを担う
            // Push the ghost's block reasons as soon as it is shown; callers only handle display and sending
            evaluation.PushBlockReasons(feedback);
            return true;
        }
    }
}
