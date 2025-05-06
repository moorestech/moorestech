using System;
using System.Collections.Generic;

namespace Game.CraftTree
{
    public class PlayerCraftTreeInfo
    {
        public Guid CurrentTargetNode { get; }
        public Dictionary<Guid, CraftTreeNode> CraftTreese { get; }
        
        public PlayerCraftTreeInfo(Guid currentTargetNode, List<CraftTreeNode> craftTrees)
        {
            CurrentTargetNode = currentTargetNode;
            CraftTreese = new Dictionary<Guid, CraftTreeNode>();
            foreach (var tree in craftTrees)
            {
                CraftTreese.Add(tree.NodeId, tree);
            }
        }
    }
}