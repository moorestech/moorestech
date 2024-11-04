using System.Collections.Generic;
using Core.Item.Interface;

namespace Game.CraftChainer
{
    public class CraftChainTreeNode : ICraftChainTreeNode
    {
        public string CraftRecipeType { get; }
        public List<CraftChainItem> ResultItems { get; }
        public Dictionary<CraftChainItem, ICraftChainTreeNode> RequiredItems { get; }
        
        public CraftChainTreeNode(string craftRecipeType, List<CraftChainItem> resultItems, Dictionary<CraftChainItem, ICraftChainTreeNode> requiredItems)
        {
            CraftRecipeType = craftRecipeType;
            ResultItems = resultItems;
            RequiredItems = requiredItems;
        }
    }
}