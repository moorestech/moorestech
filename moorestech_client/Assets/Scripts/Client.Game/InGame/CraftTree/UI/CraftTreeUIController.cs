using System;
using System.Collections.Generic;
using Client.Game.InGame.CraftTree.Manager;
using Client.Game.InGame.CraftTree.Network;
using Client.Game.InGame.CraftTree.Utility;
using Core.Item;
using Game.CraftTree.Data;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.UI
{
    /// <summary>
    /// クラフトツリーUIの表示・操作を管理するコントローラークラス
    /// </summary>
    public class CraftTreeUIController : MonoBehaviour
    {
        [SerializeField] private CraftTreeUIView _view;
        
        private ClientCraftTreeManager _craftTreeManager;
        private CraftTreeNetworkService _networkService;
        private ItemSearchService _itemSearchService;
        
        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="craftTreeManager">クラフトツリーマネージャー</param>
        /// <param name="networkService">ネットワークサービス</param>
        /// <param name="itemSearchService">アイテム検索サービス</param>
        public void Initialize(
            ClientCraftTreeManager craftTreeManager,
            CraftTreeNetworkService networkService,
            ItemSearchService itemSearchService)
        {
            _craftTreeManager = craftTreeManager ?? throw new ArgumentNullException(nameof(craftTreeManager));
            _networkService = networkService ?? throw new ArgumentNullException(nameof(networkService));
            _itemSearchService = itemSearchService ?? throw new ArgumentNullException(nameof(itemSearchService));
            
            if (_view == null)
            {
                Debug.LogError("CraftTreeUIView is not assigned!");
                return;
            }
            
            // ビューのイベントを購読
            _view.OnNodeClick += HandleNodeClick;
            _view.OnNodeRightClick += HandleNodeRightClick;
            _view.OnGoalItemSelect += HandleGoalItemSelect;
            _view.OnCloseButtonClick += HandleCloseButtonClick;
            _view.OnSearchButtonClick += HandleSearchButtonClick;
            
            // マネージャーのイベントを購読
            _craftTreeManager.OnTreeUpdated += HandleTreeUpdated;
            _craftTreeManager.OnGoalItemsUpdated += HandleGoalItemsUpdated;
        }
        
        /// <summary>
        /// クリーンアップ処理
        /// </summary>
        private void OnDestroy()
        {
            // イベント購読解除
            if (_view != null)
            {
                _view.OnNodeClick -= HandleNodeClick;
                _view.OnNodeRightClick -= HandleNodeRightClick;
                _view.OnGoalItemSelect -= HandleGoalItemSelect;
                _view.OnCloseButtonClick -= HandleCloseButtonClick;
                _view.OnSearchButtonClick -= HandleSearchButtonClick;
            }
            
            if (_craftTreeManager != null)
            {
                _craftTreeManager.OnTreeUpdated -= HandleTreeUpdated;
                _craftTreeManager.OnGoalItemsUpdated -= HandleGoalItemsUpdated;
            }
        }
        
        /// <summary>
        /// クラフトツリーUIを表示
        /// </summary>
        public void ShowCraftTreeUI()
        {
            if (_view != null)
            {
                _view.ShowUI();
                
                // 現在のツリーを表示
                if (_craftTreeManager != null && _craftTreeManager.CurrentTree != null)
                {
                    _view.RenderTree(_craftTreeManager.CurrentTree);
                }
            }
        }
        
        /// <summary>
        /// クラフトツリーUIを非表示
        /// </summary>
        public void HideCraftTreeUI()
        {
            if (_view != null)
            {
                _view.HideUI();
            }
        }
        
        /// <summary>
        /// ノードクリック時の処理
        /// </summary>
        /// <param name="node">クリックされたノード</param>
        private void HandleNodeClick(CraftTreeNode node)
        {
            if (node == null || _craftTreeManager == null)
                return;
                
            // ノードの展開を試みる
            _craftTreeManager.ExpandNode(node);
        }
        
        /// <summary>
        /// ノード右クリック時の処理
        /// </summary>
        /// <param name="node">右クリックされたノード</param>
        /// <param name="screenPosition">スクリーン座標</param>
        private void HandleNodeRightClick(CraftTreeNode node, Vector2 screenPosition)
        {
            if (node == null || _craftTreeManager == null || _view == null)
                return;
                
            // コンテキストメニューアクションを取得
            var actions = _craftTreeManager.GetContextMenuActions(node);
            
            // コンテキストメニューを表示
            _view.ShowContextMenu(actions, screenPosition);
        }
        
        /// <summary>
        /// 目標アイテム選択時の処理
        /// </summary>
        /// <param name="itemId">選択されたアイテムID</param>
        /// <param name="count">数量</param>
        private void HandleGoalItemSelect(ItemId itemId, int count)
        {
            if (_craftTreeManager == null)
                return;
                
            // 目標アイテムを設定
            _craftTreeManager.SetGoalItem(itemId, count);
        }
        
        /// <summary>
        /// 閉じるボタンクリック時の処理
        /// </summary>
        private void HandleCloseButtonClick()
        {
            HideCraftTreeUI();
        }
        
        /// <summary>
        /// 検索ボタンクリック時の処理
        /// </summary>
        private void HandleSearchButtonClick()
        {
            if (_view == null || _itemSearchService == null)
                return;
                
            // カテゴリ別のアイテム一覧を取得
            var categorizedItems = _itemSearchService.GetCategorizedItems();
            
            // 検索UIを表示
            _view.ShowSearchItemUI(categorizedItems);
        }
        
        /// <summary>
        /// ツリー更新時の処理
        /// </summary>
        private void HandleTreeUpdated()
        {
            if (_view != null && _craftTreeManager != null && _craftTreeManager.CurrentTree != null)
            {
                // ツリーの再描画
                _view.RenderTree(_craftTreeManager.CurrentTree);
            }
        }
        
        /// <summary>
        /// 目標アイテム更新時の処理
        /// </summary>
        /// <param name="goalItems">更新された目標アイテムリスト</param>
        private void HandleGoalItemsUpdated(List<GoalItem> goalItems)
        {
            // 必要に応じて追加処理
        }
        
        /// <summary>
        /// レシピ選択ダイアログ表示
        /// </summary>
        /// <param name="node">対象ノード</param>
        /// <param name="recipes">選択可能なレシピリスト</param>
        public void ShowRecipeSelectDialog(CraftTreeNode node, List<RecipeData> recipes)
        {
            if (_view == null || node == null || recipes == null)
                return;
                
            // レシピ選択モーダルを表示
            _view.ShowRecipeSelectModal(node, recipes, selectedRecipe => 
            {
                if (_craftTreeManager != null)
                {
                    _craftTreeManager.SelectRecipe(node, selectedRecipe);
                }
            });
        }
    }
}