using System;
using System.Collections.Generic;
using Core.Item;
using Game.CraftTree.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.CraftTree.UI
{
    /// <summary>
    /// 目標表示HUD（ゲーム画面左上固定）の視覚表現を担当するクラス
    /// </summary>
    public class GoalHUDView : MonoBehaviour
    {
        [SerializeField] private GameObject _goalPanel;
        [SerializeField] private Transform _goalItemsContainer;
        [SerializeField] private Transform _craftableItemsContainer;
        [SerializeField] private GoalItemUI _goalItemUIPrefab;
        [SerializeField] private Button _toggleCraftableButton;
        [SerializeField] private Text _moreItemsText;
        [SerializeField] private int _maxDisplayItems = 5;
        [SerializeField] private GameObject _craftableItemsPanel;
        [SerializeField] private Text _craftablePanelTitle;
        
        // イベント
        public event Action OnToggleCraftableItems;
        public event Action<ItemId> OnItemSelected;
        
        private void Awake()
        {
            // トグルボタンのイベント登録
            if (_toggleCraftableButton != null)
                _toggleCraftableButton.onClick.AddListener(() => OnToggleCraftableItems?.Invoke());
        }
        
        /// <summary>
        /// UIの表示
        /// </summary>
        public void ShowUI()
        {
            gameObject.SetActive(true);
            if (_goalPanel != null)
                _goalPanel.SetActive(true);
        }
        
        /// <summary>
        /// UIの非表示
        /// </summary>
        public void HideUI()
        {
            gameObject.SetActive(false);
            if (_goalPanel != null)
                _goalPanel.SetActive(false);
        }
        
        /// <summary>
        /// 目標アイテムリストの更新
        /// </summary>
        /// <param name="goalItems">表示する目標アイテムリスト</param>
        public void UpdateGoalItems(List<GoalItem> goalItems)
        {
            if (_goalItemsContainer == null || _goalItemUIPrefab == null)
                return;
                
            // 既存のアイテムUIをクリア
            ClearContainer(_goalItemsContainer);
            
            if (goalItems == null || goalItems.Count == 0)
            {
                // 目標がない場合のメッセージを表示
                ShowNoGoalsMessage();
                return;
            }
            
            // 表示上限を超える場合
            bool hasMoreItems = goalItems.Count > _maxDisplayItems;
            int displayCount = hasMoreItems ? _maxDisplayItems : goalItems.Count;
            
            // 目標アイテムUIを生成
            for (int i = 0; i < displayCount; i++)
            {
                var goalItem = goalItems[i];
                AddItemToContainer(_goalItemsContainer, goalItem);
            }
            
            // 追加アイテム表示
            if (hasMoreItems && _moreItemsText != null)
            {
                _moreItemsText.gameObject.SetActive(true);
                _moreItemsText.text = $"+ {goalItems.Count - _maxDisplayItems} 他";
            }
            else if (_moreItemsText != null)
            {
                _moreItemsText.gameObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// クラフト可能アイテムリストの更新
        /// </summary>
        /// <param name="craftableItems">表示するクラフト可能アイテムリスト</param>
        public void UpdateCraftableItems(List<GoalItem> craftableItems)
        {
            if (_craftableItemsContainer == null || _goalItemUIPrefab == null)
                return;
                
            // 既存のアイテムUIをクリア
            ClearContainer(_craftableItemsContainer);
            
            if (craftableItems == null || craftableItems.Count == 0)
            {
                if (_craftablePanelTitle != null)
                    _craftablePanelTitle.text = "クラフト可能アイテム (0)";
                return;
            }
            
            // タイトル更新
            if (_craftablePanelTitle != null)
                _craftablePanelTitle.text = $"クラフト可能アイテム ({craftableItems.Count})";
                
            // 表示上限を超える場合
            bool hasMoreItems = craftableItems.Count > _maxDisplayItems;
            int displayCount = hasMoreItems ? _maxDisplayItems : craftableItems.Count;
            
            // クラフト可能アイテムUIを生成
            for (int i = 0; i < displayCount; i++)
            {
                var craftableItem = craftableItems[i];
                AddItemToContainer(_craftableItemsContainer, craftableItem);
            }
        }
        
        /// <summary>
        /// コンテナをクリア
        /// </summary>
        /// <param name="container">クリア対象のコンテナ</param>
        private void ClearContainer(Transform container)
        {
            if (container == null)
                return;
                
            foreach (Transform child in container)
            {
                if (child.GetComponent<GoalItemUI>() != null)
                    Destroy(child.gameObject);
            }
        }
        
        /// <summary>
        /// 目標アイテムUIをコンテナに追加
        /// </summary>
        /// <param name="container">追加先コンテナ</param>
        /// <param name="goalItem">目標アイテム</param>
        private void AddItemToContainer(Transform container, GoalItem goalItem)
        {
            if (container == null || _goalItemUIPrefab == null)
                return;
                
            var itemUI = Instantiate(_goalItemUIPrefab, container);
            itemUI.SetupGoalItem(goalItem);
            
            // クリックイベント登録
            itemUI.OnClicked += () => OnItemSelected?.Invoke(goalItem.itemId);
        }
        
        /// <summary>
        /// 目標がない場合のメッセージ表示
        /// </summary>
        private void ShowNoGoalsMessage()
        {
            // メッセージを追加する実装（必要に応じて）
        }
        
        /// <summary>
        /// クラフト可能アイテムパネルの表示/非表示を設定
        /// </summary>
        /// <param name="visible">表示する場合はtrue</param>
        public void SetCraftableItemsVisibility(bool visible)
        {
            if (_craftableItemsPanel != null)
                _craftableItemsPanel.SetActive(visible);
        }
    }
}