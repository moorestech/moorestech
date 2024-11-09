using Core.Item.Interface;

namespace Game.CraftChainer.CraftNetwork
{
    public class CraftChainerItem
    {
        public CraftChainerNodeId TargetNodeId { get; } 
        
        public IItemStack ItemStack { get; }
    }
}