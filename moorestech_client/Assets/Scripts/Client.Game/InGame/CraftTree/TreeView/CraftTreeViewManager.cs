using System;
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Core.Master;
using Game.CraftTree;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.CraftTree.TreeView
{
    public class CraftTreeViewManager : MonoBehaviour
    {
        [SerializeField] private Button hideButton;
        
        [SerializeField] private CraftTreeEditorView craftTreeEditorView;
        [SerializeField] private CraftTreeList craftTreeList;
        
        private readonly List<CraftTreeNode> _craftTreeNodes = new();
        
        private void Awake()
        {
            hideButton.onClick.AddListener(Hide);
            craftTreeList.OnNodeSelected.Subscribe(OnNodeSelected);
            craftTreeList.OnNodeDeleted.Subscribe(OnNodeDeleted);
        }
        
        [Inject]
        public void Construct(ItemRecipeViewerDataContainer itemRecipe)
        {
            craftTreeEditorView.Initialize(itemRecipe);
        }
        
        public void Show(ItemId resultItemId)
        {
            var rootNode = new CraftTreeNode(resultItemId, 1);
            craftTreeEditorView.Show(rootNode);
            gameObject.SetActive(true);
            
            _craftTreeNodes.Add(rootNode);
            craftTreeList.UpdateList(_craftTreeNodes);
        }
        
        public void Hide()
        {
            gameObject.SetActive(false);
        }
        
        
        private void OnNodeSelected(CraftTreeNode craftTreeNode)
        {
            craftTreeEditorView.Show(craftTreeNode);
        }
        
        private void OnNodeDeleted(CraftTreeNode craftTreeNode)
        {
            _craftTreeNodes.Remove(craftTreeNode);
            craftTreeList.UpdateList(_craftTreeNodes);
        }

    }
}