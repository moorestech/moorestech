using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;

namespace Server.Protocol.PacketResponse.Util.ConnectTool
{
    /// <summary>
    /// 複数素材コストの所持検証・消費・返却スタック生成をまとめる
    /// Bundles ownership check, consumption, and refund-stack creation for multi-material costs
    /// </summary>
    public static class ConnectToolMaterialConsumer
    {
        // 建設コスト等の(ItemId,個数)列を予約素材の形へ変換する
        // Convert a (ItemId,count) sequence (e.g. construction cost) into reserved materials
        public static IReadOnlyList<ConnectToolMaterialCost> ToMaterials(IReadOnlyList<(ItemId itemId, int count)> itemCounts)
        {
            if (itemCounts == null) return Array.Empty<ConnectToolMaterialCost>();
            var list = new List<ConnectToolMaterialCost>(itemCounts.Count);
            foreach (var (itemId, count) in itemCounts) list.Add(new ConnectToolMaterialCost(itemId, count));
            return list;
        }

        // 予約リスト中の同一アイテム数を合計する。判定・不足算出が必要数へ上乗せする唯一の定義
        // The single definition of the reserved amount per item that judgements and shortage calculations add on top
        public static int SumReserved(IReadOnlyList<ConnectToolMaterialCost> reservedMaterials, ItemId itemId)
        {
            if (reservedMaterials == null) return 0;
            var reserved = 0;
            foreach (var reservedMaterial in reservedMaterials)
            {
                if (reservedMaterial.ItemId == itemId) reserved += reservedMaterial.Count;
            }
            return reserved;
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

        // 素材ごとにスロット順で減算する
        // Subtract each material across inventory slots in order
        public static void Consume(IReadOnlyList<ConnectToolMaterialCost> materials, IOpenableInventory inventory)
        {
            if (materials == null) return;
            foreach (var material in materials)
            {
                if (material.Count <= 0 || material.ItemId == ItemMaster.EmptyItemId) continue;
                ElectricWireSystemUtil.ConsumeItem(inventory, material.ItemId, material.Count);
            }
        }

        // 返却用のアイテムスタック列を生成する
        // Create refund item stacks for the given materials
        public static List<IItemStack> CreateRefundItems(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            var result = new List<IItemStack>();
            if (materials == null) return result;
            foreach (var material in materials)
            {
                if (material.Count <= 0 || material.ItemId == ItemMaster.EmptyItemId) continue;
                result.Add(ServerContext.ItemStackFactory.Create(material.ItemId, material.Count));
            }
            return result;
        }
    }
}
