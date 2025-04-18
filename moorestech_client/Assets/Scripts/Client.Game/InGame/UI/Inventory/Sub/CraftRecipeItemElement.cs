using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    /// <summary>
    /// クラフトレシピ一覧で表示する各レシピアイテム要素
    /// </summary>
    public class CraftRecipeItemElement : MonoBehaviour
    {
        [Header("矢印")]
        [SerializeField] private ProgressArrowView progressArrow;
        public ProgressArrowView ProgressArrowView => progressArrow;
        
        [Header("UI部品")]
        [SerializeField] private RectTransform materialParent;
        [SerializeField] private RectTransform resultParent;
        [SerializeField] private Button recipeSelectButton;
        [SerializeField] private TMP_Text craftTimeText;
        [SerializeField] private Image backgroundImage;
        
        [Header("UI設定")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = new Color(0.9f, 0.9f, 1.0f);
        [SerializeField] private Color disabledColor = new Color(0.7f, 0.7f, 0.7f);
        
        [Header("Prefab")]
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        public CraftRecipeMasterElement CraftRecipe { get; private set; }
        public bool IsCraftable { get; private set; }
        
        public IObservable<CraftRecipeItemElement> OnSelected => _onSelectedSubject;
        private readonly Subject<CraftRecipeItemElement> _onSelectedSubject = new();
        
        private readonly List<ItemSlotObject> _materialSlots = new();
        private ItemSlotObject _resultSlot;
        
        private bool _isSelected = false;
        
        public void Initialize(CraftRecipeMasterElement craftRecipe, bool isCraftable)
        {
            CraftRecipe = craftRecipe;
            IsCraftable = isCraftable;
            
            ClearSlots();
            SetupMaterialSlots();
            SetupResultSlot();
            
            // アイテム名と製作時間を設定
            craftTimeText.text = $"{craftRecipe.CraftTime}秒";
            
            // クラフト可能かどうかで見た目を変更
            backgroundImage.color = isCraftable ? normalColor : disabledColor;
            
            // ボタンクリック時のイベント
            recipeSelectButton.onClick.RemoveAllListeners();
            recipeSelectButton.onClick.AddListener(() => {
                if (IsCraftable)
                {
                    _onSelectedSubject.OnNext(this);
                }
            });
            
            SetSelected(false);
        }
        
        private void ClearSlots()
        {
            foreach (var slot in _materialSlots)
            {
                Destroy(slot.gameObject);
            }
            _materialSlots.Clear();
            
            if (_resultSlot != null)
            {
                Destroy(_resultSlot.gameObject);
                _resultSlot = null;
            }
        }
        
        private void SetupMaterialSlots()
        {
            foreach (var requiredItem in CraftRecipe.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                
                var slot = Instantiate(itemSlotObjectPrefab, materialParent);
                var toolTipText = $"{itemViewData.ItemName}\n必要数: {requiredItem.Count}";
                slot.SetItem(itemViewData, requiredItem.Count, toolTipText);
                slot.SetFrame(ItemSlotFrameType.CraftRecipe);
                _materialSlots.Add(slot);
            }
        }
        
        private void SetupResultSlot()
        {
            var itemViewData = ClientContext.ItemImageContainer.GetItemView(CraftRecipe.CraftResultItemGuid);
            _resultSlot = Instantiate(itemSlotObjectPrefab, resultParent);
            _resultSlot.SetItem(itemViewData, CraftRecipe.CraftResultCount);
            _resultSlot.SetFrame(ItemSlotFrameType.Normal);
        }
        
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            backgroundImage.color = selected ? selectedColor : (IsCraftable ? normalColor : disabledColor);
        }
        
        public void UpdateCraftableState(bool isCraftable)
        {
            IsCraftable = isCraftable;
            backgroundImage.color = _isSelected ? selectedColor : (IsCraftable ? normalColor : disabledColor);
        }
        
        private void OnDestroy()
        {
            _onSelectedSubject.Dispose();
        }
    }
}