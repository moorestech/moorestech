using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Item.Interface;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    /// <summary>
    /// 橋脚1セルの最終的な設置可否を決める
    /// Settles the final placeability of a single pier cell
    /// </summary>
    internal static class TrainRailPierPlaceability
    {
        // 橋脚単体設置用。不足行は可否を落とす前に積む（Reporterの契約）
        // For standalone pier placement; shortage lines are pushed before placeability drops (the reporter's contract)
        internal static void ApplyCostShortage(PlaceInfo placeInfo, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            var placeInfos = new List<PlaceInfo> { placeInfo };
            ConstructionMaterialShortageReporter.ReportShortages(placeInfos, placeInfo.BlockId, walletQuery, inventoryItems, feedback);
            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(placeInfos, placeInfo.BlockId, walletQuery, inventoryItems);
        }

        // 接続モード用。橋脚とレールは1リクエストなので、レール側が不可なら橋脚も不可
        // For connect mode; the pier and the rail travel in one request, so a failed rail judgement blocks the pier too
        internal static void ApplyConnectJudgement(PlaceInfo placeInfo, TrainRailConnectPreviewData previewData)
        {
            if (!previewData.IsPlaceable) placeInfo.Placeable = false;
        }
    }
}
