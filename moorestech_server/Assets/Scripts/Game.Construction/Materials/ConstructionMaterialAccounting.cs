using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Game.Construction
{
    /// <summary>
    /// 素材コストの所持集計・必要数合算・充足判定を行うドメイン中立の会計
    /// Domain-neutral accounting that tallies held items, sums the requirement per item and judges affordability
    /// サーバーとクライアントはこの1箇所を共有し、判定と不足表示が同じ合計を見ることを保証する
    /// The server and the client share this single definition so judgements and shortage displays always see the same totals
    /// </summary>
    public static class ConstructionMaterialAccounting
    {
        // 所持スタック列をitemId別の所持数へ集計する唯一の供給点
        // The single supply point tallying held counts per itemId from a sequence of item stacks
        public static Dictionary<ItemId, int> TallyHeld(IEnumerable<IItemStack> inventoryItems)
        {
            var heldByItem = new Dictionary<ItemId, int>();
            foreach (var stack in inventoryItems)
            {
                heldByItem.TryGetValue(stack.Id, out var current);
                heldByItem[stack.Id] = current + stack.Count;
            }
            return heldByItem;
        }

        // 必要数をItemId単位で合算し予約分を1回だけ上乗せする唯一の定義。素材の初出順を保つ
        // The single definition summing the requirement per ItemId and adding the reservation exactly once, in first-seen order
        // 同一アイテムが複数エントリに割れていても、判定と不足表示が同じ合計を見ることを保証する
        // Guarantees the judgement and the shortage display see the same total even when one item is split across entries
        public static List<(ItemId itemId, int count)> SumRequiredByItem(IReadOnlyList<ConnectToolMaterialCost> materials, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            var requiredByItem = new Dictionary<ItemId, int>();
            var itemOrder = new List<ItemId>();
            if (materials != null)
            {
                foreach (var material in materials)
                {
                    if (!requiredByItem.ContainsKey(material.ItemId))
                    {
                        requiredByItem[material.ItemId] = SumReserved(reservedMaterials, material.ItemId);
                        itemOrder.Add(material.ItemId);
                    }
                    requiredByItem[material.ItemId] += material.Count;
                }
            }

            var required = new List<(ItemId itemId, int count)>(itemOrder.Count);
            foreach (var itemId in itemOrder) required.Add((itemId, requiredByItem[itemId]));
            return required;
        }

        // 各素材の所持合計が、予約分を上乗せした必要数を満たすか。可否判定の正本
        // Whether the summed held count of each material meets its requirement plus the reservation; the canonical affordability judgement
        public static bool HasEnough(IReadOnlyList<ConnectToolMaterialCost> materials, IReadOnlyDictionary<ItemId, int> heldByItem, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            foreach (var (itemId, required) in SumRequiredByItem(materials, reservedMaterials))
            {
                heldByItem.TryGetValue(itemId, out var held);
                if (held < required) return false;
            }
            return true;
        }

        // 所持スタック列版。集計してから正本へ委ねる
        // The item-stack version; tallies first and delegates to the canonical judgement
        public static bool HasEnough(IReadOnlyList<ConnectToolMaterialCost> materials, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            if (materials == null) return true;
            return HasEnough(materials, TallyHeld(inventoryItems), reservedMaterials);
        }

        // 予約リスト中の同一アイテム数を合計する。必要数へ上乗せする分の定義
        // Sums the reserved amount of one item; this is what the requirement adds on top
        private static int SumReserved(IReadOnlyList<ConnectToolMaterialCost> reservedMaterials, ItemId itemId)
        {
            if (reservedMaterials == null) return 0;
            var reserved = 0;
            foreach (var reservedMaterial in reservedMaterials)
            {
                if (reservedMaterial.ItemId == itemId) reserved += reservedMaterial.Count;
            }
            return reserved;
        }
    }
}
