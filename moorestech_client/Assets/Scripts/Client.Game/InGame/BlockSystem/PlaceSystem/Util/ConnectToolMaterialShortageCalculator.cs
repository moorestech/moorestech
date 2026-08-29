using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Server.Protocol.PacketResponse.Util.ConnectTool;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 接続ツール（電線・歯車チェーン・レール）の必要素材を所持と突き合わせ、不足素材だけを返す
    /// Matches a connect tool's (wire / gear chain / rail) required materials against held counts and returns only the shortages
    /// 必要数の算出はサーバーと共有のConnectToolCostCalculator、突き合わせはConstructionCostShortageCalculatorに委ねる
    /// The requirement comes from the server-shared ConnectToolCostCalculator and the match from ConstructionCostShortageCalculator
    /// </summary>
    public static class ConnectToolMaterialShortageCalculator
    {
        /// <summary>
        /// 接続距離から必要素材を算出して不足を返す。マスタに無い接続ツールでは空を返し、呼び出し元が汎用文言へ落とす
        /// Derives the requirement from the connection distance; an unknown connect tool yields empty so callers fall back to the generic wording
        /// </summary>
        public static List<ConstructionMaterialShortage> Calculate(Guid connectToolGuid, float distance, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            if (!ConnectToolCostCalculator.TryCalculate(connectToolGuid, distance, out var materials)) return new List<ConstructionMaterialShortage>();
            return Calculate(materials, ConstructionMaterialHeldCounts.Tally(inventoryItems), reservedMaterials);
        }

        /// <summary>
        /// 算出済みの必要素材と所持数から不足を返す。予約分（建設コスト等）はサーバーのEvaluatorと同じく必要数へ上乗せする
        /// Returns the shortages for an already-computed requirement; reservations (e.g. construction cost) are added on top exactly as the server evaluators do
        /// </summary>
        public static List<ConstructionMaterialShortage> Calculate(IReadOnlyList<ConnectToolMaterialCost> materials, IReadOnlyDictionary<ItemId, int> heldByItem, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            if (materials == null) return new List<ConstructionMaterialShortage>();

            var requiredItems = new List<(ItemId itemId, int count)>(materials.Count);
            foreach (var material in materials) requiredItems.Add((material.ItemId, material.Count + SumReserved(material.ItemId)));

            return ConstructionCostShortageCalculator.ToShortages(ConstructionCostShortageCalculator.CalculateRequirements(requiredItems, heldByItem));

            #region Internal

            int SumReserved(ItemId itemId)
            {
                // 予約リスト中の同一アイテム数を合計する
                // Sum the reserved amount of the same item in the reservation list
                if (reservedMaterials == null) return 0;
                var reserved = 0;
                foreach (var reservedMaterial in reservedMaterials)
                {
                    if (reservedMaterial.ItemId == itemId) reserved += reservedMaterial.Count;
                }
                return reserved;
            }

            #endregion
        }
    }
}
