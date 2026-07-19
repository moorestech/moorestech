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

        // 各素材の所持合計が必要数を満たすか
        // Whether the summed held count of each material meets its requirement
        public static bool HasEnough(IReadOnlyList<ConnectToolMaterialCost> materials, IReadOnlyList<IItemStack> inventoryItems)
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
                if (total < material.Count) return false;
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
