using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
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
        [SerializeField] private GameObject selectedFrame;
        
        public CraftRecipeMasterElement CraftRecipe { get; private set; }
        public bool IsCraftable { get; private set; }
        
        public IObservable<CraftRecipeItemElement> OnSelected => _onSelectedSubject;
        private readonly Subject<CraftRecipeItemElement> _onSelectedSubject = new();
        
        public IObservable<ItemSlotObject> OnClickMaterialItem => _onClickMaterialItem;
        private readonly Subject<ItemSlotObject> _onClickMaterialItem = new();
        
        private readonly List<ItemSlotObject> _materialSlots = new();
        private ItemSlotObject _resultSlot;
        
        private void Awake()
        {
            recipeSelectButton.onClick.AddListener(() => {
                _onSelectedSubject.OnNext(this);
            });
        }
        
        public void Initialize(CraftRecipeMasterElement craftRecipe, bool isCraftable)
        {
            CraftRecipe = craftRecipe;
            IsCraftable = isCraftable;
            
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
                    
                    var slot = Instantiate(ItemSlotObject.Prefab, materialParent);
                    var toolTipText = $"{itemViewData.ItemName}\n必要数: {requiredItem.Count}\n<size=25>クリックでこのアイテムの\nレシピを確認</size>";
                    slot.SetItem(itemViewData, requiredItem.Count, toolTipText);
                    slot.SetFrameType(ItemSlotFrameType.CraftRecipe);
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
                _resultSlot = Instantiate(ItemSlotObject.Prefab, resultParent);
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
            foreach (var slot in _materialSlots)
            {
                slot.SetGrayOut(!isCraftable);
            }
            _resultSlot.SetGrayOut(!isCraftable);
            
            IsCraftable = isCraftable;
        }
        
        private void OnDestroy()
        {
            _onSelectedSubject.Dispose();
        }
    }
}