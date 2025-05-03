using System;
using System.Collections.Generic;
using Client.Game.InGame.CraftTree.Manager;
using Game.CraftTree.Data;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.UI
{
    /// <summary>
    /// 目標表示HUD（ゲーム画面左上固定）のUIコントローラー
    /// </summary>
    public class GoalHUDUIController : MonoBehaviour
    {
        [SerializeField] private GoalHUDView _view;
        [SerializeField] private bool _showCraftableItemsByDefault = true;
        
        private ClientCraftTreeManager _craftTreeManager;
        private bool _showCraftableItems;
        
        /// <summary>
        /// 初期化
        /// </summary>
        /// <param name="craftTreeManager">クラフトツリーマネージャー</param>
        public void Initialize(ClientCraftTreeManager craftTreeManager)
        {
            _craftTreeManager = craftTreeManager ?? throw new ArgumentNullException(nameof(craftTreeManager));
            
            if (_view == null)
            {
                Debug.LogError("GoalHUDView is not assigned!");
                return;
            }
            
            // マネージャーのイベントを購読
            _craftTreeManager.OnGoalItemsUpdated += HandleGoalItemsUpdated;
            _craftTreeManager.OnCraftableItemsUpdated += HandleCraftableItemsUpdated;
            
            // ビューのイベントを購読
            _view.OnToggleCraftableItems += HandleToggleCraftableItems;
            _view.OnItemSelected += HandleItemSelected;
            
            // 初期状態を設定
            _showCraftableItems = _showCraftableItemsByDefault;
            _view.SetCraftableItemsVisibility(_showCraftableItems);
        }
        
        /// <summary>
        /// クリーンアップ処理
        /// </summary>
        private void OnDestroy()
        {
            // イベント購読解除
            if (_craftTreeManager != null)
            {
                _craftTreeManager.OnGoalItemsUpdated -= HandleGoalItemsUpdated;
                _craftTreeManager.OnCraftableItemsUpdated -= HandleCraftableItemsUpdated;
            }
            
            if (_view != null)
            {
                _view.OnToggleCraftableItems -= HandleToggleCraftableItems;
                _view.OnItemSelected -= HandleItemSelected;
            }
        }
        
        /// <summary>
        /// 表示/非表示の切り替え
        /// </summary>
        /// <param name="show">表示する場合はtrue</param>
        public void Show(bool show)
        {
            if (_view != null)
            {
                if (show)
                    _view.ShowUI();
                else
                    _view.HideUI();
            }
        }
        
        /// <summary>
        /// クラフト可能アイテムの表示/非表示を切り替え
        /// </summary>
        public void ToggleCraftableItemsVisibility()
        {
            _showCraftableItems = !_showCraftableItems;
            
            if (_view != null)
                _view.SetCraftableItemsVisibility(_showCraftableItems);
        }
        
        #region イベントハンドラ
        
        /// <summary>
        /// 目標アイテム更新時の処理
        /// </summary>
        /// <param name="goalItems">更新された目標アイテムリスト</param>
        private void HandleGoalItemsUpdated(List<GoalItem> goalItems)
        {
            if (_view != null)
                _view.UpdateGoalItems(goalItems);
        }
        
        /// <summary>
        /// クラフト可能アイテム更新時の処理
        /// </summary>
        /// <param name="craftableItems">更新されたクラフト可能アイテムリスト</param>
        private void HandleCraftableItemsUpdated(List<GoalItem> craftableItems)
        {
            if (_view != null)
                _view.UpdateCraftableItems(craftableItems);
        }
        
        /// <summary>
        /// クラフト可能アイテム表示切り替え時の処理
        /// </summary>
        private void HandleToggleCraftableItems()
        {
            ToggleCraftableItemsVisibility();
        }
        
        /// <summary>
        /// アイテム選択時の処理
        /// </summary>
        /// <param name="itemId">選択されたアイテムID</param>
        private void HandleItemSelected(Core.Item.ItemId itemId)
        {
            // 選択されたアイテムを中心にクラフトツリーを表示
            // 通常、関連するコントローラーを呼び出す（例：CraftTreeUIController）
            Debug.Log($"Selected goal item: {itemId}");
            
            // 実際の実装では、例えばイベントを発行してCraftTreeUIControllerに通知する
            // OnItemSelectedForTreeView?.Invoke(itemId);
        }
        
        #endregion
    }
}