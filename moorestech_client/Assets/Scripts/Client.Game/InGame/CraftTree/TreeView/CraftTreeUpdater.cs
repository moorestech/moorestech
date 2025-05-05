using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.Inventory.Main;
using Game.CraftTree;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public interface ICraftTreeObservable
    {
        IObservable<Unit> OnChangeNodeTarget { get; }
        IObservable<CraftTreeNode> OnUpdateNodeState { get; }
    }
    
    public class CraftTreeUpdater : ICraftTreeObservable
    {
        private readonly ILocalPlayerInventory _localPlayerInventory;
        
        public IObservable<Unit> OnChangeNodeTarget => _onChangeNodeTarget;
        private readonly Subject<Unit> _onChangeNodeTarget = new();
        
        /// <summary>
        /// value : ステータスが更新されたノード
        /// </summary>
        public IObservable<CraftTreeNode> OnUpdateNodeState => _onUpdateNodeState;
        private readonly Subject<CraftTreeNode> _onUpdateNodeState = new();
        
        private CraftTreeNode _currentRootNode;
        private List<(CraftTreeNode node, int startItemCount)> _currentTargetNodes;
        
        public CraftTreeUpdater(ILocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
            _localPlayerInventory.OnItemChange.Subscribe(_ => UpdateNodeState());
        }
        
        public void SetRootNode(CraftTreeNode node)
        {
            _currentRootNode = node;
            _currentTargetNodes = new List<(CraftTreeNode node, int startItemCount)>();
            
            var targetNodes = GetCurrentTarget(node);
            foreach (var targetNode in targetNodes)
            {
                // 始まったときのインベントリのアイテム数を取得
                var startItemCount = _localPlayerInventory.GetMainInventoryItemCount(targetNode.TargetItemId);
                _currentTargetNodes.Add((targetNode, startItemCount));
            }
            
            _onChangeNodeTarget.OnNext(Unit.Default);
        }
        
        private void UpdateNodeState()
        {
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
                
                // ステータスが更新されたノードを通知
                _onUpdateNodeState.OnNext(node);
            }
            
            if (completedCount != _currentTargetNodes.Count) return;
            
            // 全て完了していた場合目標を変更
            SetRootNode(_currentRootNode);
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