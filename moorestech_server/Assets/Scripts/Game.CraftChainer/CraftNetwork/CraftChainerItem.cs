using Core.Item.Interface;

namespace Game.CraftChainer.CraftNetwork
{
    public class CraftChainerItem
    {
        public CraftChainerNodeId TargetNodeId { get; } // TODO クラフト中にネットワークが変更されたときのことを考えておく
        
        public IItemStack ItemStack { get; }
    }
}