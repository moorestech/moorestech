using System;
using System.Collections.Generic;
using System.Linq;
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
                var currentTarget = _craftTreeUpdater.CurrentRootNode;
                if (currentTarget == null || node.NodeId == currentTarget.NodeId)
                {
                    craftTreeTargetManager.SetCurrentCraftTree(node);
                    _craftTreeUpdater.SetRootNode(node);
                }
                
                ClientContext.VanillaApi.SendOnly.SendCraftTreeNode(_craftTreeUpdater.CurrentRootNode.NodeId, _craftTreeNodes);
            }
            
            #endregion
        }
        
        [Inject]
        public void Construct(InitialHandshakeResponse initialHandshakeResponse, ItemRecipeViewerDataContainer itemRecipe, ILocalPlayerInventory localPlayerInventory)
        {
            craftTreeEditorView.Initialize(itemRecipe);
            _craftTreeUpdater = new CraftTreeUpdater(localPlayerInventory);
            craftTreeTargetManager.Initialize(_craftTreeUpdater);
            Initialize(initialHandshakeResponse.CraftTree);
            
            #region MyRegion
            
            void Initialize(CraftTreeResponse craftTreeResponse)
            {
                if (craftTreeResponse == null || craftTreeResponse.CraftTrees == null || craftTreeResponse.CraftTrees.Count == 0)
                {
                    return;
                }
                
                // サーバーから取得したツリーをセット
                _craftTreeNodes.AddRange(craftTreeResponse.CraftTrees);
                craftTreeList.UpdateList(_craftTreeNodes);
                
                // ターゲットノードがある場合はそれをアクティブにする
                if (craftTreeResponse.CurrentTargetNode == Guid.Empty) return;
                
                // ターゲットノードを検索
                var targetNode = _craftTreeNodes.FirstOrDefault(node => node.NodeId == craftTreeResponse.CurrentTargetNode);
                if (targetNode == null) return;
                
                // エディターにセット
                craftTreeEditorView.SetEditor(targetNode);
                
                // 目標表示を更新
                _craftTreeUpdater.SetRootNode(targetNode);
                craftTreeTargetManager.SetCurrentCraftTree(targetNode);
            }
            
  #endregion
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