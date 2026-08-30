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
            // 必要数の合算は可否判定と同じ正本を通す。集計単位が割れると可否と表示が食い違う
            // The requirement goes through the very definition the affordability judgement uses; a differing unit would split verdict from display
            var requiredItems = ConnectToolMaterialConsumer.SumRequiredByItem(materials, reservedMaterials);
            return ConstructionCostShortageCalculator.ToShortages(ConstructionCostShortageCalculator.CalculateRequirements(requiredItems, heldByItem));
        }
    }
}
