using System;
using System.Collections.Generic;
using System.Linq;
using Game.CraftTree;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeUpdater
    {
        /// <summary>
        /// value : 新しいターゲットとなるノード
        /// </summary>
        public IObservable<CraftTreeNode> OnChangeNodeTarget => _onChangeNodeTarget;
        private readonly Subject<CraftTreeNode> _onChangeNodeTarget = new();
        
        /// <summary>
        /// value : ステータスが更新されたノード
        /// </summary>
        public IObservable<CraftTreeNode> OnUpdateNodeState => _onUpdateNodeState;
        private readonly Subject<CraftTreeNode> _onUpdateNodeState = new();
        
        private CraftTreeNode _currentRootNode;
        private List<CraftTreeNode> _currentTargetNodes;
        
        public void SetRootNode(CraftTreeNode node)
        {
            _currentRootNode = node;
            _currentTargetNodes = new List<CraftTreeNode>();
            
            var targetNode = GetCurrentTarget(node);
        }
        
        public void ManualUpdate()
        {
            
        }
        
        public static List<CraftTreeNode> GetCurrentTarget(CraftTreeNode rootNode)
        {
            var (targetNode, _) = GetDeepestIncompleteNode(rootNode, 0);
            
            if (targetNode.Parent == null)
            {
                // 親がないので最後のルートノードをターゲットにする
                return new List<CraftTreeNode> { targetNode };
            }
            
            // ターゲットとなる子ノードを返す
            return targetNode.Children.ToList();
            
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