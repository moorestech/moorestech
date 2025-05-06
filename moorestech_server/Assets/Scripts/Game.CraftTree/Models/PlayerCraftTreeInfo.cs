using System;
using System.Collections.Generic;

namespace Game.CraftTree.Models
{
    public class PlayerCraftTreeInfo
    {
        public Guid CurrentTargetNode { get; }
        public Dictionary<Guid, CraftTreeNode> CraftTrees { get; }
        
        public PlayerCraftTreeInfo(Guid currentTargetNode, List<CraftTreeNode> craftTrees)
        {
            CurrentTargetNode = currentTargetNode;
            CraftTrees = new Dictionary<Guid, CraftTreeNode>();
            foreach (var tree in craftTrees)
            {
                CraftTrees.Add(tree.NodeId, tree);
            }
        }
    }
}