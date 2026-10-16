using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Game.Context;

namespace Game.Block.Blocks.ConnectionLine
{
    /// <summary>
    /// 接続線の素材を返却スタックへ展開する共通部品
    /// Expands connection-line materials into refund stacks, shared by removal and disconnect
    /// </summary>
    public static class ConnectionLineRefundItems
    {
        public static List<IItemStack> Create(IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            var result = new List<IItemStack>();
            if (materials == null) return result;
            foreach (var material in materials)
            {
                // 長い線は素材数がMaxStackを超えるので、スタック上限ごとに分けて返す
                // Long lines can exceed MaxStack, so split each material into max-size stacks
                // silent-ok: 数1未満と空アイテムは返却物が無いだけ / CreateSplitStacksが空リストを返し総数テストで固定
                result.AddRange(ServerContext.ItemStackFactory.CreateSplitStacks(material.ItemId, material.Count));
            }
            return result;
        }
    }
}
