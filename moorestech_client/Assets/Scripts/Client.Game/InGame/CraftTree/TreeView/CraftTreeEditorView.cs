using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Game.CraftTree.Models;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeEditorView : MonoBehaviour
    {
        [SerializeField] private CraftTreeEditorNodeItem nodePrefab;
        [SerializeField] private RectTransform content;
        [SerializeField] private VerticalLayoutGroup layoutGroup;
        
        public IObservable<Unit> OnTreeUpdated => _onTreeUpdated;
        private readonly Subject<Unit> _onTreeUpdated = new();
        
        public CraftTreeNode CurrentRootNode { get; private set; }
        
        private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        private readonly List<CraftTreeEditorNodeItem> _nodes = new();
        
        public void Initialize(ItemRecipeViewerDataContainer itemRecipe)
        {
            _itemRecipeViewerDataContainer = itemRecipe;
        }
        
        public void UpdateEditor()
        {
            if (CurrentRootNode == null)
            {
                return;
            }
            
            SetEditor(CurrentRootNode);
        }
        
        public void SetEditor(CraftTreeNode rootNode)
        {
            DestroyNodes();
            CreateNode(rootNode, 0);
            
            CurrentRootNode = rootNode;
            
            // reference : https://medium.com/@sakastudio100/the-problem-of-a-missingreferenceexception-occurring-when-a-child-of-verticallayoutgroup-is-deleted-c2153b8ae311
            layoutGroup.CalculateLayoutInputHorizontal();
            layoutGroup.CalculateLayoutInputVertical();
            
            #region Internal
            
            void CreateNode(CraftTreeNode node, int depth)
            {
                var nodeView = Instantiate(nodePrefab, content);
                nodeView.OnUpdateNode.Subscribe(_ =>
                {
                    _onTreeUpdated.OnNext(Unit.Default);
                    SetEditor(rootNode);
                });
                
                foreach (var child in node.Children)
                {
                    CreateNode(child, depth + 1);
                }
                
                nodeView.Initialize(node, depth, _itemRecipeViewerDataContainer);
                _nodes.Add(nodeView);
            }
            
            #endregion
            
        }
        
        public void DestroyNodes()
        {
            foreach (var node in _nodes)
            {
                Destroy(node.gameObject);
            }
            CurrentRootNode = null;
            _nodes.Clear();
        }
    }
}