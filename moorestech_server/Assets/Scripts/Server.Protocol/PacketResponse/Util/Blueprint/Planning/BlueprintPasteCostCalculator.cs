using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Construction;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public static class BlueprintPasteCostCalculator
    {
        public static List<(ItemId itemId, int count)> CalcRequiredItems(IReadOnlyList<BlueprintPasteCopyDraft> drafts, ConstructionWalletQuery wallet, bool isPaymentWaived)
        {
            // ブロックは種類ごとのセル数を数え、財布の残りを考慮したコストセット数で払う。無料設置ではブロックと電線は払わない
            // Count cells per block kind and pay the wallet-aware cost sets; free placement pays for neither blocks nor wires
            var cellCounts = new Dictionary<BlockId, int>();
            var lineMaterials = new List<ConnectToolMaterialCost>();
            foreach (var draft in drafts)
            {
                for (var i = 0; i < draft.Elements.Count; i++)
                {
                    if (!draft.NonOverlapFlags[i]) continue;
                    // 坂と直線の残数を二重使用しない
                    // Share one remaining balance across slope and straight variants
                    var blockId = ConstructionWalletQuery.ResolveWalletBlockId(draft.Elements[i].BlockId);
                    cellCounts.TryGetValue(blockId, out var count);
                    cellCounts[blockId] = count + 1;
                }

                // 無料設置でもチェーンは払う（#1486 は電線ツールだけを無料にした）
                // Chains are paid even in free placement (#1486 made only the electric wire tool free)
                foreach (var line in draft.Lines)
                {
                    if (isPaymentWaived && line.Kind == BlueprintPasteLineKind.ElectricWire) continue;
                    lineMaterials.AddRange(line.Materials);
                }
            }

            var required = new Dictionary<ItemId, int>();
            if (!isPaymentWaived)
            {
                foreach (var (blockId, cellCount) in cellCounts)
                {
                    var sets = wallet.GetRequiredCostSets(blockId, cellCount);
                    foreach (var (itemId, count) in ConstructionCostItems.ToItemCounts(MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems)) Add(itemId, count * sets);
                }
            }

            // 配線素材は既存の合算定義へ委ねる
            // Line materials go through the existing summation definition
            foreach (var (itemId, count) in ConstructionMaterialAccounting.SumRequiredByItem(lineMaterials, Array.Empty<ConnectToolMaterialCost>())) Add(itemId, count);
            return required.Select(kv => (kv.Key, kv.Value)).ToList();

            #region Internal

            void Add(ItemId itemId, int count)
            {
                // 財布で賄えた素材は表示行に含めない
                // Wallet-covered materials do not produce display rows
                if (count != 0)
                {
                    required.TryGetValue(itemId, out var current);
                    required[itemId] = current + count;
                }
            }

            #endregion
        }
    }
}
