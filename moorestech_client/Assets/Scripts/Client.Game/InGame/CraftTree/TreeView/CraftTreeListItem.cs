using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Game.CraftTree.Models;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeListItem : MonoBehaviour
    {
        [SerializeField] private ItemSlotView itemSlotView;
        [SerializeField] private TMP_Text itemNameText;
        
        [SerializeField] private Button selectButton;
        [SerializeField] private Button deleteButton;
        
        public IObservable<CraftTreeNode> OnNodeSelected => _onNodeSelected;
        private readonly Subject<CraftTreeNode> _onNodeSelected = new();
        
        public IObservable<CraftTreeNode> OnNodeDeleted => _onNodeDeleted;
        private readonly Subject<CraftTreeNode> _onNodeDeleted = new();
        
        private CraftTreeNode _craftTreeNode;
        
        private void Awake()
        {
            selectButton.onClick.AddListener(() =>
            {
                _onNodeSelected.OnNext(_craftTreeNode);
            });
            deleteButton.onClick.AddListener(() =>
            {
                _onNodeDeleted.OnNext(_craftTreeNode);
            });
        }
        
        
        public void Initialize(CraftTreeNode craftTreeNode)
        {
            _craftTreeNode = craftTreeNode;
            var itemViewData = ClientContext.ItemImageContainer.GetItemView(craftTreeNode.TargetItemId);
            itemNameText.text = $"{itemViewData.ItemName}";
            itemSlotView.SetItem(itemViewData, 0);
        }
    }
}