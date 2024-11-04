using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Game.CraftChainer
{
    public interface ICraftChainTreeNode
    {
        public const string CraftRecipe = "CraftRecipe";
        public const string MachineRecipe = "MachineRecipe";
        
        public string CraftRecipeType { get; }
        
        public List<CraftChainItem> ResultItems { get; }
        public Dictionary<CraftChainItem,ICraftChainTreeNode> RequiredItems{ get; }
    }
}