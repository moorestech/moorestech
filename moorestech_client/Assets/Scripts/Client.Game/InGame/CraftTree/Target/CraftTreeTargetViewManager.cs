using System.Collections.Generic;
using Game.CraftTree;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Target
{
    public class CraftTreeTargetViewManager : MonoBehaviour
    {
        [SerializeField] private CraftTreeTargetView targetView;
        
        public void SetCurrentCraftTree(CraftTreeNode rootNode)
        {
            // 最も深い未完了のノードを取得する
            var (targetNode, _) = GetDeepestIncompleteNode(rootNode, 0);
            
            if (targetNode.Parent == null)
            {
                targetView.SetFinalTarget(targetNode);
            }
            else
            {
                // 未完了のターゲットの親をセットする
                targetView.SetTarget(targetNode.Parent);
            }
            
            #region Internal
            
            (CraftTreeNode node,int depth) GetDeepestIncompleteNode(CraftTreeNode searchNode, int currentDepth)
            {
                if (searchNode.Children.Count == 0)
                {
                    return (searchNode, currentDepth);
                }

                var childDepths = new List<(CraftTreeNode node, int depth)>();
                foreach (var child in searchNode.Children)
                {
                    if (!child.IsCompleted)
                    {
                        childDepths.Add(GetDeepestIncompleteNode(child, currentDepth + 1));
                    }
                }
                
                if (childDepths.Count == 0)
                {
                    return (searchNode, currentDepth);
                }
                
                // 子ノードの中で最も深いノードを取得
                var deepestChild = childDepths[0];
                for (int i = 1; i < childDepths.Count; i++)
                {
                    if (childDepths[i].depth > deepestChild.depth)
                    {
                        deepestChild = childDepths[i];
                    }
                }
                return deepestChild;
            }
            
  #endregion
        }
    }
}