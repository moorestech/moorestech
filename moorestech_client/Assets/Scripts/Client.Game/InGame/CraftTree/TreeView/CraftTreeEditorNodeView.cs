using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.ContextMenu;
using Game.CraftTree;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorNodeView : MonoBehaviour
    {
        [SerializeField] private RectTransform uiTransform;
        [SerializeField] private float depthWidth = 50f;
        
        [SerializeField] private GameObject contextMenu;
        
        [SerializeField] private UGuiContextMenuTarget contextMenuTarget;
        
        public IObservable<Unit> OnUpdateNode => _onUpdateNode;
        private readonly Subject<Unit> _onUpdateNode = new();
        
        public CraftTreeNode Node { get; private set; }
        private List<CraftTreeEditorNodeView> _children;
        
        public void Initialize(List<CraftTreeEditorNodeView> children, CraftTreeNode node, int depth)
        {
            var position = uiTransform.anchoredPosition;
            position.x = depth * depthWidth;
            uiTransform.anchoredPosition = position;
            
            Node = node;
            _children = children;
            
            var contextMenus = new List<ContextMenuBarInfo>
            {
                new("レシピを展開", ExpandNode),
                new("レシピを非表示", HideChildrenNode),
            };
            contextMenuTarget.SetContextMenuBars(contextMenus);
        }
        
        private void ExpandNode()
        {
            
        }
        
        private void HideChildrenNode()
        {
            
        }
    }
}