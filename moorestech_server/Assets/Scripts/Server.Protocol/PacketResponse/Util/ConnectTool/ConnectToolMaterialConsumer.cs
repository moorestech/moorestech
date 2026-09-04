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
    /// 接続ツール素材の予約変換・消費・返却スタック生成をまとめる
    /// Bundles reservation conversion, consumption and refund-stack creation for connect tool materials
    /// 所持集計・必要数合算・充足判定はドメイン中立のGame.Construction.ConstructionMaterialAccountingが正本
    /// The tally, the per-item requirement and the affordability judgement live in the domain-neutral Game.Construction.ConstructionMaterialAccounting
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
