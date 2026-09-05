using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.UI.Inventory.Main;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ベルト列の建設コスト不足を報告し、賄えないセルを設置不可にする
    /// Reports construction-cost shortages for a belt run and marks unaffordable cells unplaceable
    /// </summary>
    public static class BeltConveyorConstructionCostApplier
    {
        // ファミリー内は建設コストと設置数/1セットが一致する（マスタ検証済み）ので先頭の設置可セルを代表にする
        // Cost and placementsPerCost match within a family (validated at master load), so the first placeable cell is representative
        public static void Apply(List<PlaceInfo> placeInfos, ConstructionWalletQuery constructionWalletQuery, ILocalPlayerInventory localPlayerInventory, PlacementFeedback feedback)
        {
            var representativeIndex = placeInfos.FindIndex(info => info.Placeable);
            if (representativeIndex < 0) return;

            var representativeBlockId = placeInfos[representativeIndex].BlockId;
            ConstructionMaterialShortageReporter.ReportShortages(placeInfos, representativeBlockId, constructionWalletQuery, localPlayerInventory, feedback);
            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(placeInfos, representativeBlockId, constructionWalletQuery, localPlayerInventory);
        }
    }
}
