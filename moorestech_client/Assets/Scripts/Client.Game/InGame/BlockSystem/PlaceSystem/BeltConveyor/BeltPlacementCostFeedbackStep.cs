using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Replace.Cost;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor
{
    /// <summary>
    ///     ベルト列のコスト可否・不足理由・プレビュー色をまとめて決める
    ///     Decides a belt run's affordability, its shortage reasons and its preview colors in one step
    ///     張替えを含む列はサーバーと同じセル逐次評価、含まない列は通常設置と同じバッチ評価に分かれる
    ///     A run with replace cells is judged cell by cell like the server does, and one without falls to the same batch check as normal placement
    /// </summary>
    public static class BeltPlacementCostFeedbackStep
    {
        public static void ApplyCostAndUpdateColors(List<PlaceInfo> placeInfos, BlockGameObjectDataStore blockGameObjectDataStore, ConstructionWalletQuery walletQuery, ILocalPlayerInventory localPlayerInventory, IPlacementPreviewBlockGameObjectController previewBlockController, PlacementFeedback feedback)
        {
            // 直線・坂・分岐器・張替えで列にBlockIdが混ざるため、コストはセル自身のBlockIdごとに数える
            // Straight, slope, splitter and replace cells mix BlockIds in one run, so the cost is counted per the cell's own BlockId
            // 張替えを含む列はサーバーがセル1つずつ「返却→支払い」を判定するので、同じ順で1パス回して可否を出す
            // A run with replace cells is judged refund-then-pay one cell at a time by the server, so one pass in that same order produces the verdict
            var replaceSimulation = BeltReplaceCostSimulator.TrySimulate(placeInfos, blockGameObjectDataStore, walletQuery, localPlayerInventory);
            if (replaceSimulation == null)
            {
                // 不足表示はPlaceableを落とす前に読む（落とした後では不足が消えて理由を出せない）
                // The shortage display reads before Placeable is cleared (afterwards the shortage disappears and no reason can be shown)
                ConstructionMaterialShortageReporter.ReportShortages(placeInfos, walletQuery, localPlayerInventory, feedback);
                ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(placeInfos, walletQuery, localPlayerInventory);
                previewBlockController.UpdatePlaceableColors(placeInfos);
                return;
            }

            // 送信されるセルを「素材が足りない」と説明しないため、課金元不明のセルは不足集計から外す
            // A cell that is going to be sent is never explained as short of materials, so the cells with an unknown payer stay out of the tally
            // 不足は「所持品＋実際に届いた返却品」で見るため、シミュレーション結果の素材を渡す
            // The shortage sees the holdings plus the refunds that actually landed, so the simulated materials go in
            ConstructionMaterialShortageReporter.ReportShortages(replaceSimulation.CollectCertainCells(placeInfos), walletQuery, replaceSimulation.CostCheckItems, feedback);
            replaceSimulation.MarkUnaffordableCellsAsNotPlaceable();

            // 最終的なPlaceable状態でプレビュー色を更新してから、不確実なセルだけ色を差し替える
            // The preview colors follow the final Placeable state, and only then do the uncertain cells get their own color
            previewBlockController.UpdatePlaceableColors(placeInfos);
            replaceSimulation.ApplyUncertainRefundColors(previewBlockController);
        }
    }
}
