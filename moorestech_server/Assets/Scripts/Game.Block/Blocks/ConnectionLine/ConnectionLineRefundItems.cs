using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// 接続線の素材を返却スタックへ展開する。電線・チェーンの撤去返却と切断返却が同じ規則を使う
    /// Expand connection-line materials into refund stacks; removal and disconnect refunds share this one rule
    /// </summary>
    public static class ConnectionLineRefundItems
    {
        public static List<IItemStack> Create(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            var result = new List<IItemStack>();
            if (materials == null) return result;
            foreach (var material in materials)
            {
                // 数0と空アイテムは返さない
                // Skip zero counts and the empty item
                if (material.Count <= 0 || material.ItemId == ItemMaster.EmptyItemId) continue;
                result.Add(ServerContext.ItemStackFactory.Create(material.ItemId, material.Count));
            }
            return result;
        }
    }
}
