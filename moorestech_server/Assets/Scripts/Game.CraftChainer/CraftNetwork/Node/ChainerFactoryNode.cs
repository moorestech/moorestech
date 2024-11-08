using System.Collections.Generic;
using Game.CraftChainer.CraftChain;

namespace Game.CraftChainer.CraftNetwork.Node
{
    public class ChainerFactoryNode : ICraftChainerNode
    {
        public CraftChainerNodeId NodeId { get; }
        public List<ICraftChainerNode> ConnectTargets { get; }
        public CraftingSolverRecipe CraftingSolverRecipe { get; private set; }
        
        
        public ChainerFactoryNode(CraftChainerNodeId nodeId)
        {
            NodeId = nodeId;
        }
        
        public void InsertItem(CraftChainerItem craftChainerItem)
        {
            throw new System.NotImplementedException();
        }
        
        public void SetCraftingSolverRecipe(CraftingSolverRecipe craftingSolverRecipe)
        {
            CraftingSolverRecipe = craftingSolverRecipe;
        }
    }
}