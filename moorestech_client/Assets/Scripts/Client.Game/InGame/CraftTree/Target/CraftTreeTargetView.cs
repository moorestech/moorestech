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
        
        public void SetTarget(CraftTreeNode node)
        {
            ClearTarget();
            
            // 新しいターゲットをセット
            var targetItem = Instantiate(targetViewItemsPrefab, itemParent);
            targetItem.Initialize(node, 0);
            _targetViewItems.Add(targetItem);

            // 子ノードもセット
            foreach (var child in node.Children)
            {
                var childItem = Instantiate(targetViewItemsPrefab, itemParent);
                childItem.Initialize(child, 1);
                _targetViewItems.Add(childItem);
            }
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