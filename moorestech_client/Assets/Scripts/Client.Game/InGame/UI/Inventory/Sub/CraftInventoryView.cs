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
        
        private readonly List<ItemSlotObject> _craftMaterialSlotList = new();
        private ItemSlotObject _craftResultSlot;
        private int _currentCraftingConfigIndex;
        
        private CraftRecipeMasterElement[] _currentCraftRecipes;
        
        private ILocalPlayerInventory _localPlayerInventory;
        
        [Inject]
        public void Construct(ILocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
            

            
            nextRecipeButton.onClick.AddListener(() =>
            {
                _currentCraftingConfigIndex++;
                if (_currentCraftRecipes.Length <= _currentCraftingConfigIndex) _currentCraftingConfigIndex = 0;
                DisplayRecipe(_currentCraftingConfigIndex);
            });
            
            prevRecipeButton.onClick.AddListener(() =>
            {
                _currentCraftingConfigIndex--;
                if (_currentCraftingConfigIndex < 0) _currentCraftingConfigIndex = _currentCraftRecipes.Length - 1;
                DisplayRecipe(_currentCraftingConfigIndex);
            });
            
            craftButton.OnCraftFinish.Subscribe(_ =>
            {
                if (_currentCraftRecipes == null || _currentCraftRecipes.Length == 0)
                {
                    return;
                }
                
                ClientContext.VanillaApi.SendOnly.Craft(_currentCraftRecipes[_currentCraftingConfigIndex].CraftRecipeGuid);
            }).AddTo(this);
        }
        
        public void OnClickItemList(ItemSlotObject slot)
        {
            _currentCraftRecipes = MasterHolder.CraftRecipeMaster.GetResultItemCraftRecipes(slot.ItemViewData.ItemId);
            if (_currentCraftRecipes.Length == 0) return;
            
            _currentCraftingConfigIndex = 0;
            DisplayRecipe(0);
        }
        
        
        private void DisplayRecipe(int index)
        {
            var craftingConfigInfo = _currentCraftRecipes[index];
            
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
                foreach (var requiredItem in craftingConfigInfo.RequiredItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                    
                    var itemSlotObject = Instantiate(itemSlotObjectPrefab, craftMaterialParent);
                    itemSlotObject.SetItem(itemViewData, requiredItem.Count);
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                    _craftMaterialSlotList.Add(itemSlotObject);
                }
            }
            
            void SetResultSlot()
            {
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(craftingConfigInfo.CraftResultItemGuid);
                _craftResultSlot = Instantiate(itemSlotObjectPrefab, craftResultParent);
                _craftResultSlot.SetItem(itemViewData, craftingConfigInfo.CraftResultCount);
            }
            
            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = _currentCraftRecipes.Length != 1;
                nextRecipeButton.interactable = _currentCraftRecipes.Length != 1;
                recipeCountText.text = $"{_currentCraftingConfigIndex + 1} / {_currentCraftRecipes.Length}";
                craftButton.SetInteractable(IsCraftable(craftingConfigInfo));
                
                var itemName = MasterHolder.ItemMaster.GetItemMaster(craftingConfigInfo.CraftResultItemGuid).Name;
                itemNameText.text = itemName;
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