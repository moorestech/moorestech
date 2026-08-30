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

        // 各素材の所持合計が、予約分を上乗せした必要数を満たすか
        // Whether the summed held count of each material meets its requirement plus the reservation
        public static bool HasEnough(IReadOnlyList<ConnectToolMaterialCost> materials, IReadOnlyList<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)
        {
            if (materials == null) return true;
            foreach (var material in materials)
            {
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != material.ItemId) continue;
                    total += stack.Count;
                }
                if (total < material.Count + SumReserved(reservedMaterials, material.ItemId)) return false;
            }
            return true;
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
