using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.CraftTree.Target;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Network.API;
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
        [SerializeField] private Button showCraftTreeListButton;
        [SerializeField] private Button hideButton;
        [SerializeField] private Button setTargetButton;
        
        [SerializeField] private CraftTreeTargetViewManager craftTreeTargetManager;
        [SerializeField] private CraftTreeEditorView craftTreeEditorView;
        [SerializeField] private CraftTreeList craftTreeList;
        
        private readonly List<CraftTreeNode> _craftTreeNodes = new();
        private CraftTreeUpdater _craftTreeUpdater;
        
        private void Awake()
        {
            craftTreeList.OnNodeSelected.Subscribe(OnNodeSelected);
            craftTreeList.OnNodeDeleted.Subscribe(OnNodeDeleted);
            
            showCraftTreeListButton.onClick.AddListener(Show);
            hideButton.onClick.AddListener(Hide);
            
            craftTreeEditorView.OnTreeUpdated.Subscribe(UpdateTreeTarget);
            setTargetButton.onClick.AddListener(() =>
            {
                var currentRootNode = craftTreeEditorView.CurrentRootNode;
                if (currentRootNode == null)
                {
                    return;
                }
                UpdateTreeTarget(currentRootNode);
            });
            
            #region Internal
            
            void UpdateTreeTarget(CraftTreeNode node)
            {
                craftTreeTargetManager.SetCurrentCraftTree(node);
                _craftTreeUpdater.SetRootNode(node);
                
                ClientContext.VanillaApi.SendOnly.SendCraftTreeNode(node);
            }
            
            #endregion
        }
        
        [Inject]
        public void Construct(ItemRecipeViewerDataContainer itemRecipe, ILocalPlayerInventory localPlayerInventory)
        {
            craftTreeEditorView.Initialize(itemRecipe);
            _craftTreeUpdater = new CraftTreeUpdater(localPlayerInventory);
            craftTreeTargetManager.Initialize(_craftTreeUpdater);
        }
        
        public void CreateNewCraftTree(ItemId resultItemId)
        {
            var rootNode = new CraftTreeNode(resultItemId, 1, null);
            craftTreeEditorView.SetEditor(rootNode);
            _craftTreeNodes.Add(rootNode);
            craftTreeList.UpdateList(_craftTreeNodes);
            
            Show();
        }
        
        private void Show()
        {
            gameObject.SetActive(true);
            craftTreeEditorView.UpdateEditor();
        }
        
        public void Hide()
        {
            gameObject.SetActive(false);
        }
        
        
        private void OnNodeSelected(CraftTreeNode craftTreeNode)
        {
            craftTreeEditorView.SetEditor(craftTreeNode);
        }
        
        private void OnNodeDeleted(CraftTreeNode craftTreeNode)
        {
            _craftTreeNodes.Remove(craftTreeNode);
            craftTreeList.UpdateList(_craftTreeNodes);
            craftTreeEditorView.DestroyNodes();
        }
        
        private void Update()
        {
            setTargetButton.interactable = craftTreeEditorView.CurrentRootNode != null;
        }
    }
}