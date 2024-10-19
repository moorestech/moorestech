using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using Mooresmaster.Model.CraftRecipesModule;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    //TODO CraftITemViewにリネーム
    public class CraftInventoryView : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform craftMaterialParent;
        [SerializeField] private RectTransform craftResultParent;
        
        [SerializeField] private CraftButton craftButton;
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text recipeCountText;
        
        public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        
        private readonly List<ItemSlotObject> _craftMaterialSlotList = new();
        private ItemSlotObject _craftResultSlot;
        private ILocalPlayerInventory _localPlayerInventory;
        private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        private int CraftRecipeCount => _currentItemRecipes.CraftRecipes.Count;
        private RecipeViewerItemRecipes _currentItemRecipes;
        private int _currentIndex;
        
        [Inject]
        public void Construct(ILocalPlayerInventory localPlayerInventory, ItemRecipeViewerDataContainer itemRecipeViewerDataContainer)
        {
            _itemRecipeViewerDataContainer = itemRecipeViewerDataContainer;
            _localPlayerInventory = localPlayerInventory;
            _localPlayerInventory.OnItemChange.Subscribe(_ =>
            {
                if (_currentItemRecipes != null)
                {
                    UpdateCraftButton(_currentItemRecipes.CraftRecipes[_currentIndex]);
                }
            });
            
            nextRecipeButton.onClick.AddListener(() =>
            {
                _currentIndex++;
                if (CraftRecipeCount <= _currentIndex) _currentIndex = 0;
                DisplayRecipe(_currentIndex);
            });
            
            prevRecipeButton.onClick.AddListener(() =>
            {
                _currentIndex--;
                if (_currentIndex < 0) _currentIndex = CraftRecipeCount - 1;
                DisplayRecipe(_currentIndex);
            });
            
            craftButton.OnCraftFinish.Subscribe(_ =>
            {
                if (_currentItemRecipes == null || CraftRecipeCount == 0)
                {
                    return;
                }
                
                var currentCraftGuid = _currentItemRecipes.CraftRecipes[_currentIndex].CraftRecipeGuid;
                ClientContext.VanillaApi.SendOnly.Craft(currentCraftGuid);
            }).AddTo(this);
        }
        
        private void UpdateCraftButton(CraftRecipeMasterElement craftRecipe)
        {
            craftButton.SetInteractable(IsCraftable(craftRecipe));
        }
        
        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
            _currentItemRecipes = recipeViewerItemRecipes;
            _currentIndex = 0;
        }
        
        public void DisplayRecipe(int index)
        {
            var craftRecipe = _currentItemRecipes.CraftRecipes[index];
            
            ClearSlotObject();
            
            SetMaterialSlot();
            
            SetResultSlot();
            
            UpdateButtonAndText();
            
            #region InternalMethod
            
            void ClearSlotObject()
            {
                foreach (var materialSlot in _craftMaterialSlotList) Destroy(materialSlot.gameObject);
                _craftMaterialSlotList.Clear();
                if (_craftResultSlot != null) Destroy(_craftResultSlot.gameObject);
            }
            
            void SetMaterialSlot()
            {
                foreach (var requiredItem in craftRecipe.RequiredItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                    
                    var itemSlotObject = Instantiate(itemSlotObjectPrefab, craftMaterialParent);
                    itemSlotObject.SetItem(itemViewData, requiredItem.Count);
                    _craftMaterialSlotList.Add(itemSlotObject);
                    
                    // 原材料をクリックしたときにそのレシピを表示するようにする
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickMaterialItem);
                }
            }
            
            void SetResultSlot()
            {
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(craftRecipe.CraftResultItemGuid);
                _craftResultSlot = Instantiate(itemSlotObjectPrefab, craftResultParent);
                _craftResultSlot.SetItem(itemViewData, craftRecipe.CraftResultCount);
            }
            
            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = CraftRecipeCount != 1;
                nextRecipeButton.interactable = CraftRecipeCount != 1;
                recipeCountText.text = $"{_currentIndex + 1} / {CraftRecipeCount}";
                craftButton.SetCraftTime(craftRecipe.CraftTime);
                UpdateCraftButton(craftRecipe);
                
                var itemName = MasterHolder.ItemMaster.GetItemMaster(craftRecipe.CraftResultItemGuid).Name;
                itemNameText.text = itemName;
            }
            
            void OnClickMaterialItem(ItemSlotObject itemSlotObject)
            {
                var itemId = itemSlotObject.ItemViewData.ItemId;
                var itemRecipes = _itemRecipeViewerDataContainer.GetItem(itemId);
                _onClickItem.OnNext(itemRecipes);
            }
            
            #endregion
        }
        
        /// <summary>
        ///     そのレシピがクラフト可能かどうかを返す
        ///     この処理はある1つのレシピに対してのみ使い、一気にすべてのアイテムがクラフト可能かチェックするには<see cref="IsAllItemCraftable" />を用いる
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
    }
}