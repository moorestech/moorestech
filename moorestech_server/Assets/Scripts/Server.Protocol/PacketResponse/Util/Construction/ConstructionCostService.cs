using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 建設コスト(requiredItems)の消費。充足判定と返却生成はGame.ConstructionのConstructionCostRulesが持つ
    /// Consumes construction costs (requiredItems); the affordability judgement and the refund stacks belong to Game.Construction's ConstructionCostRules
    /// </summary>
    public static class ConstructionCostService
    {
        public static void ConsumeRequiredItems(IReadOnlyList<(ItemId itemId, int count)> itemCounts, IOpenableInventory inventory)
        {
            if (itemCounts == null || itemCounts.Count == 0) return;

            // 先頭スロットから順に減算する共通処理（電線消費と同一実装）を再利用する
            // Reuse the shared first-slot-onward consumption logic used by wire consumption
            foreach (var (itemId, count) in itemCounts)
            {
                ElectricWireSystemUtil.ConsumeItem(inventory, itemId, count);
            }
        }
    }
}
