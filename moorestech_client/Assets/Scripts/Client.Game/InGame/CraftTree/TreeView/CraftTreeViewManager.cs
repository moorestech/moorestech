using System;
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
        public void Construct(ItemRecipeViewerDataContainer itemRecipe, ILocalPlayerInventory localPlayerInventory, InitialHandshakeResponse initialHandshakeResponse)
        {
            craftTreeEditorView.Initialize(itemRecipe);
            _craftTreeUpdater = new CraftTreeUpdater(localPlayerInventory);
            craftTreeTargetManager.Initialize(_craftTreeUpdater);
            
            // 初期クラフトツリーデータがあれば設定する
            if (initialHandshakeResponse.CraftTreeNodes != null && initialHandshakeResponse.CraftTreeNodes.Count > 0)
            {
                SetInitialCraftTreeData(initialHandshakeResponse.CraftTreeTargetNodeId, initialHandshakeResponse.CraftTreeNodes);
            }
        }
        
        public void CreateNewCraftTree(ItemId resultItemId)
        {
            var rootNode = new CraftTreeNode(resultItemId, 1, null);
            craftTreeEditorView.SetEditor(rootNode);
            _craftTreeNodes.Add(rootNode);
            craftTreeList.UpdateList(_craftTreeNodes);
            
            Show();
        }
        
        /// <summary>
        /// 初期化時にクラフトツリーデータをセットするメソッド
        /// </summary>
        /// <param name="targetNodeId">現在のターゲットノードID</param>
        /// <param name="craftTreeNodes">クラフトツリーのノードリスト</param>
        public void SetInitialCraftTreeData(Guid targetNodeId, List<CraftTreeNode> craftTreeNodes)
        {
            if (craftTreeNodes == null || craftTreeNodes.Count == 0)
            {
                return; // データがない場合は何もしない
            }
            
            // クラフトツリーノードをセット
            _craftTreeNodes.Clear();
            _craftTreeNodes.AddRange(craftTreeNodes);
            craftTreeList.UpdateList(_craftTreeNodes);
            
            // ターゲットノードをセット
            if (targetNodeId != Guid.Empty)
            {
                // クラフトツリーノードからターゲットノードを検索
                foreach (var node in craftTreeNodes)
                {
                    if (node.NodeId == targetNodeId)
                    {
                        craftTreeEditorView.SetEditor(node);
                        _craftTreeUpdater?.SetRootNode(node);
                        craftTreeTargetManager.SetCurrentCraftTree(node);
                        break;
                    }
                }
            }
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