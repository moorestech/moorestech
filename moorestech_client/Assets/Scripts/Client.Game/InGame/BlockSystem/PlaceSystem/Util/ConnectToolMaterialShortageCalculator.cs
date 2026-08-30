using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Server.Protocol.PacketResponse.Util.ConnectTool;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 接続ツールの必要素材と所持を突き合わせ不足を返す
    /// Matches a connect tool's required materials against held counts and returns the shortages
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
            foreach (var material in materials) requiredItems.Add((material.ItemId, RequiredCount(material, reservedMaterials)));

            return ConstructionCostShortageCalculator.ToShortages(ConstructionCostShortageCalculator.CalculateRequirements(requiredItems, heldByItem));
        }

        /// <summary>
        /// 不足が1件でもあるかだけを返す。可否判定だけが要る呼び出し元がリストを作って捨てないための入口
        /// Returns only whether anything falls short, so affordability-only callers never build a list to throw away
        /// </summary>
        public static bool HasAnyShortage(IReadOnlyList<ConnectToolMaterialCost> materials, IReadOnlyDictionary<ItemId, int> heldByItem, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            if (materials == null) return false;

            foreach (var material in materials)
            {
                heldByItem.TryGetValue(material.ItemId, out var held);
                if (held < RequiredCount(material, reservedMaterials)) return true;
            }
            return false;
        }

        // 予約分を上乗せした必要数。CalculateとHasAnyShortageで式を1つに保つ
        // The requirement with the reservation added on top, keeping one formula for Calculate and HasAnyShortage
        private static int RequiredCount(ConnectToolMaterialCost material, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            return material.Count + ConnectToolMaterialConsumer.SumReserved(reservedMaterials, material.ItemId);
        }
    }
}
