using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.ContextMenu;
using Client.Game.InGame.UI.Inventory.Common;
using Game.CraftTree;
using TMPro;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorNodeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private ItemSlotObject itemSlotObject;
        
        [SerializeField] private RectTransform offsetUiTransform;
        [SerializeField] private float depthWidth = 50f;
        
        [SerializeField] private UGuiContextMenuTarget contextMenuTarget;
        
        public IObservable<Unit> OnUpdateNode => _onUpdateNode;
        private readonly Subject<Unit> _onUpdateNode = new();
        
        public CraftTreeNode Node { get; private set; }
        private List<CraftTreeEditorNodeView> _children;
        
        public void Initialize(List<CraftTreeEditorNodeView> children, CraftTreeNode node, int depth)
        {
            Node = node;
            _children = children;
            
            SetItem();
            SetPosition();
            SetContextMenu();
            
            #region Internal
            
            void SetItem()
            {
                var itemView = ClientContext.ItemImageContainer.GetItemView(node.TargetItemId);
                itemNameText.text = $"{itemView.ItemName}  {node.CurrentCount} / {node.RequiredCount}";
            }
            
            void SetPosition()
            {
                var position = offsetUiTransform.anchoredPosition;
                position.x = depth * depthWidth;
                offsetUiTransform.anchoredPosition = position;
            }
            
            void SetContextMenu()
            {
                var contextMenus = new List<ContextMenuBarInfo>
                {
                    new("レシピを展開", ExpandNode),
                    new("レシピを非表示", HideChildrenNode),
                };
                contextMenuTarget.SetContextMenuBars(contextMenus);
            }
            
            #endregion
        }
        
        private void ExpandNode()
        {
            
        }
        
        private void HideChildrenNode()
        {
            
        }
    }
}