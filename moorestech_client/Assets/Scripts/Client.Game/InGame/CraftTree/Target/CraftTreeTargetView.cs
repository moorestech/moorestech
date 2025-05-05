using System.Collections.Generic;
using Game.CraftTree;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Target
{
    public class CraftTreeTargetView : MonoBehaviour
    {
        [SerializeField] private CraftTreeTargetViewItems targetViewItemsPrefab;
        [SerializeField] private Transform itemParent;
        
        private readonly List<CraftTreeTargetViewItems> _targetViewItems = new();
        
        public void SetTarget(List<CraftTreeNode> nodes)
        {
            ClearTarget();
            
            var parent = nodes[0].Parent;
            if (parent != null)
            {
                // 親ノードがあるときはそれが目標になるようにセット
                SetTopTarget(parent);
                
                // 目標となるノードをセット
                foreach (var node in nodes)
                {
                    var childItem = Instantiate(targetViewItemsPrefab, itemParent);
                    childItem.Initialize(node, 1);
                    _targetViewItems.Add(childItem);
                }
                
                return;
            }
            
            // 親ノードがないときはそれがそのまま最後の目標となる
            SetTopTarget(nodes[0]);
            
            
            #region Internal
            
            
            // 現状の目標となるノードをセット
            void SetTopTarget(CraftTreeNode topTarget)
            {
                var targetItem = Instantiate(targetViewItemsPrefab, itemParent);
                targetItem.Initialize(topTarget, 0);
                _targetViewItems.Add(targetItem);
            }
            
            #endregion

        }
        
        public void SetFinalTarget(CraftTreeNode node)
        {
            ClearTarget();
            
            // 新しいターゲットをセット
            var targetItem = Instantiate(targetViewItemsPrefab, itemParent);
            targetItem.Initialize(node, 0);
            _targetViewItems.Add(targetItem);
        }
        
        private void ClearTarget()
        {
            // 既存のターゲットを削除
            foreach (var item in _targetViewItems)
            {
                Destroy(item.gameObject);
            }
            _targetViewItems.Clear();
        }
    }
}