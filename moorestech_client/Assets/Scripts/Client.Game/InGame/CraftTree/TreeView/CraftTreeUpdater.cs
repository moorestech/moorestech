using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Inventory.Main;
using Game.CraftTree;
using UniRx;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeUpdater
    {
        public CraftTreeNode CurrentRootNode { get; private set; }
        
        public IObservable<CraftTreeNode> OnUpdateCraftTree => _onUpdateCraftTree;
        private readonly Subject<CraftTreeNode> _onUpdateCraftTree = new();
        
        private readonly ILocalPlayerInventory _localPlayerInventory;
        
        private List<(CraftTreeNode node, int startItemCount)> _currentTargetNodes = new();
        
        public CraftTreeUpdater(ILocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
            _localPlayerInventory.OnItemChange.Subscribe(_ => UpdateNodeState());
        }
        
        public void SetRootNode(CraftTreeNode node)
        {
            CurrentRootNode = node;
            _currentTargetNodes = new List<(CraftTreeNode node, int startItemCount)>();
            if (node == null)
            {
                return;
            }
            
            var targetNodes = GetCurrentTarget(node);
            foreach (var targetNode in targetNodes)
            {
                // 始まったときのインベントリのアイテム数を取得
                var startItemCount = _localPlayerInventory.GetMainInventoryItemCount(targetNode.TargetItemId);
                _currentTargetNodes.Add((targetNode, startItemCount));
            }
            
            _onUpdateCraftTree.OnNext(node);
        }
        
        private void UpdateNodeState()
        {
            if (CurrentRootNode == null)
            {
                return;
            }
            
            var completedCount = 0;
            foreach (var (node, startItemCount) in _currentTargetNodes)
            {
                // アイテム数が変化した場合
                var currentTotalItemCount = _localPlayerInventory.GetMainInventoryItemCount(node.TargetItemId);
                
                var currentItemCount = currentTotalItemCount - startItemCount;
                var previousItemCount = node.CurrentCount;
                
                // アイテム数が変化していない場合はスキップ
                if (currentItemCount == previousItemCount)
                {
                    continue;
                }
                
                node.SetCurrentItemCount(currentItemCount);
                completedCount += node.IsCompleted ? 1 : 0;
            }
            
            // イベントだけ発行して終了
            if (completedCount != _currentTargetNodes.Count)
            {
                _onUpdateCraftTree.OnNext(CurrentRootNode);
                return;
            }
            
            // 全て完了していた場合目標を変更
            SetRootNode(CurrentRootNode);
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
            return targetNode.Parent.Children.ToList();
            
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