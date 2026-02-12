using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Mod.Texture;
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using TMPro;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Craft
{
    public class CraftInventoryView : MonoBehaviour
    {
        [SerializeField] private CraftRecipeItemElement recipeItemElementPrefab;
        [SerializeField] private RectTransform recipeListContainer;
        [SerializeField] private CraftButton craftButton;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text noRecipesText;
        
        public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        
        [Inject] private ILocalPlayerInventory _localPlayerInventory;
        [Inject] private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        private RecipeViewerItemRecipes _currentItemRecipes;
        private CraftRecipeItemElement _selectedRecipeElement;
        private readonly List<CraftRecipeItemElement> _recipeElements = new();
        
        [Inject]
        public void Construct()
        {
            _localPlayerInventory.OnItemChange.Subscribe(_ =>
            {
                if (_currentItemRecipes != null)
                {
                    UpdateRecipesCraftableState();
                }
            }).AddTo(this);
            
            craftButton.OnCraftFinish.Subscribe(_ =>
            {
                if (_currentItemRecipes == null || _selectedRecipeElement == null || !_selectedRecipeElement.IsCraftable)
                {
                    return;
                }
                
                var currentCraftGuid = _selectedRecipeElement.CraftRecipe.CraftRecipeGuid;
                ClientContext.VanillaApi.SendOnly.Craft(currentCraftGuid);
            }).AddTo(this);
        }
        
        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
            _currentItemRecipes = recipeViewerItemRecipes;
            
            // 既存のレシピ要素をクリア
            ClearRecipeElements();
            
            var unlockedRecipes = _currentItemRecipes.UnlockedCraftRecipes();
            if (unlockedRecipes.Count == 0)
            {
                noRecipesText.gameObject.SetActive(true);
                craftButton.gameObject.SetActive(false);
                itemNameText.text = "No recipes";
                return;
            }
            
            noRecipesText.gameObject.SetActive(false);
            craftButton.gameObject.SetActive(true);
            
            // 新しいレシピ要素を生成
            GenerateRecipeElements(unlockedRecipes);
            
            // 最初のレシピを選択
            if (_recipeElements.Count > 0)
            {
                SelectRecipe(_recipeElements[0]);
            }
        }
        
        private void ClearRecipeElements()
        {
            foreach (var element in _recipeElements)
            {
                Destroy(element.gameObject);
            }
            _recipeElements.Clear();
            _selectedRecipeElement = null;
        }
        
        private void GenerateRecipeElements(List<CraftRecipeMasterElement> recipes)
        {
            foreach (var recipe in recipes)
            {
                var element = Instantiate(recipeItemElementPrefab, recipeListContainer);
                var isCraftable = IsCraftable(recipe);
                element.Initialize(recipe, isCraftable, _localPlayerInventory);
                
                element.OnSelected.Subscribe(SelectRecipe).AddTo(element);
                element.OnClickMaterialItem.Subscribe(OnClickMaterialItem).AddTo(element);
                
                _recipeElements.Add(element);
            }
            
            #region Internal
            
            void OnClickMaterialItem(ItemSlotView itemSlotView)
            {
                var itemId = itemSlotView.ItemViewData.ItemId;
                var itemRecipes = _itemRecipeViewerDataContainer.GetItem(itemId);
                _onClickItem.OnNext(itemRecipes);
            }
            
            #endregion
        }
        
        private void SelectRecipe(CraftRecipeItemElement element)
        {
            // 以前の選択を解除
            if (_selectedRecipeElement != null)
            {
                _selectedRecipeElement.SetSelected(false);
            }
            
            // 新しい選択を設定
            _selectedRecipeElement = element;
            _selectedRecipeElement.SetSelected(true);
            
            // クラフトボタンの状態を更新
            UpdateCraftButton();
            
            // アイテム名を更新
            var itemName = MasterHolder.ItemMaster.GetItemMaster(element.CraftRecipe.CraftResultItemGuid).Name;
            itemNameText.text = itemName;
        }
        
        private void UpdateCraftButton()
        {
            var element = _selectedRecipeElement;
            if (element != null)
            {
                craftButton.SetInteractable(element.IsCraftable);
                // クラフト時間、ProgressArrowを設定
                craftButton.SetCraftInfo(element.CraftRecipe.CraftTime, element.ProgressArrowView);
            }
            else
            {
                craftButton.SetInteractable(false);
            }
        }
        
        private void UpdateRecipesCraftableState()
        {
            foreach (var element in _recipeElements)
            {
                var isCraftable = IsCraftable(element.CraftRecipe);
                element.UpdateCraftableState(isCraftable);
            }
            
            UpdateCraftButton();
        }
        
        /// <summary>
        /// そのレシピがクラフト可能かどうかを返す
        /// </summary>
        private bool IsCraftable(CraftRecipeMasterElement craftRecipeMasterElement)
        {
            var itemPerCount = new Dictionary<ItemId, int>();
            foreach (var item in _localPlayerInventory)
            {
                if (item.Id == ItemMaster.EmptyItemId) continue;
                if (itemPerCount.ContainsKey(item.Id))
                    itemPerCount[item.Id] += item.Count;
                else
                    itemPerCount.Add(item.Id, item.Count);
            }
            
            foreach (var requiredItem in craftRecipeMasterElement.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                
                if (!itemPerCount.ContainsKey(itemId)) return false;
                if (itemPerCount[itemId] < requiredItem.Count) return false;
            }
            
            return true;
        }
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
        public static string GetMaterialTolTip(ItemViewData itemViewData)
        {
            var tooltipText = ItemSlotView.GetToolTipText(itemViewData);
            var craftRecipes = MasterHolder.CraftRecipeMaster.GetResultItemCraftRecipes(itemViewData.ItemId);
            
            // レシピがなければそのまま返す
            if (craftRecipes.Length == 0) return tooltipText;
            
            // レシピがあればテキストを追加
            tooltipText += $"\n<size=25>Click to view\nrecipes for this item</size>";
            
            return tooltipText;
        }
    }
}