using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Mod.Texture;
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Craft
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
        [SerializeField] private GameObject selectedFrame;
        
        public CraftRecipeMasterElement CraftRecipe { get; private set; }
        public bool IsCraftable { get; private set; }
        
        public IObservable<CraftRecipeItemElement> OnSelected => _onSelectedSubject;
        private readonly Subject<CraftRecipeItemElement> _onSelectedSubject = new();
        
        public IObservable<ItemSlotView> OnClickMaterialItem => _onClickMaterialItem;
        private readonly Subject<ItemSlotView> _onClickMaterialItem = new();
        
        private readonly List<ItemSlotView> _materialSlots = new();
        private readonly List<(ItemId itemId, int requiredCount)> _materialRequirements = new();
        private ItemSlotView _resultSlot;
        private ILocalPlayerInventory _localPlayerInventory;
        
        private void Awake()
        {
            recipeSelectButton.onClick.AddListener(() => {
                _onSelectedSubject.OnNext(this);
            });
        }
        
        public void Initialize(CraftRecipeMasterElement craftRecipe, bool isCraftable, ILocalPlayerInventory localPlayerInventory)
        {
            CraftRecipe = craftRecipe;
            IsCraftable = isCraftable;
            _localPlayerInventory = localPlayerInventory;
            
            SetupMaterialSlots();
            SetupResultSlot();
            
            // アイテム名と製作時間を設定
            craftTimeText.text = $"{craftRecipe.CraftTime}秒";
            
            SetSelected(false);
            UpdateCraftableState(isCraftable);
            
            #region Internal
            
            
            void SetupMaterialSlots()
            {
                foreach (var requiredItem in CraftRecipe.RequiredItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                    
                    // 所持数を計算
                    var ownedCount = GetItemCount(itemId);
                    
                    // 材料の要求情報を保存
                    _materialRequirements.Add((itemId, requiredItem.Count));
                    
                    var slot = Instantiate(ItemSlotView.Prefab, materialParent);
                    
                    // 表示を更新
                    UpdateMaterialSlotDisplay(slot, itemViewData, ownedCount, requiredItem.Count);
                    
                    _materialSlots.Add(slot);
                    
                    slot.OnLeftClickUp.Subscribe(_ =>
                    {
                        _onClickMaterialItem.OnNext(slot);
                    });
                }
            }
            
            void SetupResultSlot()
            {
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(CraftRecipe.CraftResultItemGuid);
                _resultSlot = Instantiate(ItemSlotView.Prefab, resultParent);
                _resultSlot.SetItem(itemViewData, CraftRecipe.CraftResultCount);
                _resultSlot.SetFrameType(ItemSlotFrameType.Normal);
            }
            
            #endregion
        }
        
        
        public void SetSelected(bool selected)
        {
            selectedFrame.SetActive(selected);
        }
        
        public void UpdateCraftableState(bool isCraftable)
        {
            IsCraftable = isCraftable;
            
            for (var i = 0; i < _materialSlots.Count; i++)
            {
                var slot = _materialSlots[i];
                var (itemId, requiredCount) = _materialRequirements[i];
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                
                // 所持数を計算
                var ownedCount = GetItemCount(itemId);
                
                // 表示を更新
                UpdateMaterialSlotDisplay(slot, itemViewData, ownedCount, requiredCount);
            }
            
            // 結果スロットのグレーアウト状態も更新
            _resultSlot.SetGrayOut(!IsCraftable);
        }
        
        private void UpdateMaterialSlotDisplay(ItemSlotView slot, ItemViewData itemViewData, int ownedCount, int requiredCount)
        {
            var countText = CreateCountText(ownedCount, requiredCount);
            var toolTipText = CreateToolTipText(itemViewData.ItemName, ownedCount, requiredCount);
            
            slot.SetItem(itemViewData, 0, toolTipText);
            slot.SetCountTextFontSize(17);
            
            // カウントテキストを直接設定
            var commonSlot = slot.GetComponent<CommonSlotView>();
            commonSlot.SetView(itemViewData.ItemImage, countText, toolTipText);
            
            slot.SetFrameType(ItemSlotFrameType.CraftRecipe);
            
            // 個別のアイテムが不足している場合はグレーアウト
            slot.SetGrayOut(ownedCount < requiredCount);
            
            #region Internal
            
            string CreateCountText(int ownedCount, int requiredCount)
            {
                return ownedCount < requiredCount
                    ? $"<color=red>{ownedCount}</color>/{requiredCount}"
                    : $"{ownedCount}/{requiredCount}";
            }
            
            string CreateToolTipText(string itemName, int ownedCount, int requiredCount)
            {
                return $"{itemName}\n所持数: {ownedCount}\n必要数: {requiredCount}\n<size=25>クリックでこのアイテムの\nレシピを確認</size>";
            }
            
            #endregion
        }
        
        private int GetItemCount(ItemId itemId)
        {
            var count = 0;
            foreach (var item in _localPlayerInventory)
            {
                if (item.Id == itemId)
                {
                    count += item.Count;
                }
            }
            return count;
        }
        
        private void OnDestroy()
        {
            _onSelectedSubject.Dispose();
        }
    }
}