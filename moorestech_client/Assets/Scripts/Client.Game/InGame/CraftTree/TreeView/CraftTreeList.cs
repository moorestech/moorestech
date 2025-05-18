using System;
using System.Collections.Generic;
using Game.CraftTree.Models;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeList : MonoBehaviour
    {
        [SerializeField] private CraftTreeListItem craftTreeListItemPrefab;
        [SerializeField] private Transform craftTreeNodeParent;
        
        public IObservable<CraftTreeNode> OnNodeSelected => _onNodeSelected;
        private readonly Subject<CraftTreeNode> _onNodeSelected = new();
        
        public IObservable<CraftTreeNode> OnNodeDeleted => _onNodeDeleted;
        private readonly Subject<CraftTreeNode> _onNodeDeleted = new();
        
        private readonly List<CraftTreeListItem> _craftTreeListItems = new();
        
        public void UpdateList(List<CraftTreeNode> craftTrees)
        {
            ClearList();
            CreateListItem();
            
            #region Internal
            
            void ClearList()
            {
                foreach (var item in _craftTreeListItems)
                {
                    Destroy(item.gameObject);
                }
                
                _craftTreeListItems.Clear();
            }
            
            void CreateListItem()
            {
                foreach (var node in craftTrees)
                {
                    var listItem = Instantiate(craftTreeListItemPrefab, craftTreeNodeParent);
                    listItem.Initialize(node);
                    _craftTreeListItems.Add(listItem);
                    
                    listItem.OnNodeSelected.Subscribe(n =>
                    {
                        _onNodeSelected.OnNext(n);
                    });
                    listItem.OnNodeDeleted.Subscribe(n =>
                    {
                        _onNodeDeleted.OnNext(n);
                    });
                }
            }
            
  #endregion
        }
    }
}