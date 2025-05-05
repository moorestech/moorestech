using System.Collections.Generic;
using System.Linq;

namespace Game.CraftTree
{
    public class CraftTreeManager
    {
        private readonly Dictionary<int,List<CraftTreeNode>> _craftTree = new();
        private readonly Dictionary<int, CraftTreeNode> _usingCraftTree = new();
        
        public void ApplyCraftTree(int playerId, CraftTreeNode craftTree)
        {
            _usingCraftTree[playerId] = craftTree;
            if (!_craftTree.TryGetValue(playerId, out List<CraftTreeNode> currentCraftTrees))
            {
                _craftTree.Add(playerId, new List<CraftTreeNode> { craftTree });
                return;
            }
            
            var isReplaced = false;
            foreach (var tree in currentCraftTrees.Where(tree => tree.NodeId == craftTree.NodeId))
            {
                // 既存のクラフトツリーを削除して新しいものを追加
                isReplaced = true;
                currentCraftTrees.Remove(tree);
                currentCraftTrees.Add(craftTree);
                break;
            }
            
            if (!isReplaced)
            {
                currentCraftTrees.Add(craftTree);
            }
        }
        
    }
}