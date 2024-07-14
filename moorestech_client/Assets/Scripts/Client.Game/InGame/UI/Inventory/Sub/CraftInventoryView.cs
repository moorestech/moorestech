using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Const;
using Game.Context;
using Game.Crafting.Interface;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class CraftInventoryView : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform craftMaterialParent;
        [SerializeField] private RectTransform craftResultParent;
        
        [SerializeField] private RectTransform itemListParent;
        
        [SerializeField] private CraftButton craftButton;
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text recipeCountText;
        
        private readonly List<ItemSlotObject> _craftMaterialSlotList = new();
        private readonly List<ItemSlotObject> _itemListObjects = new();
        private ItemSlotObject _craftResultSlot;
        private int _currentCraftingConfigIndex;
        
        private IReadOnlyList<CraftingConfigInfo> _currentCraftingConfigInfos;
        
        private ILocalPlayerInventory _localPlayerInventory;
        
        [Inject]
        public void Construct(ILocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
            _localPlayerInventory.OnItemChange.Subscribe(OnItemChange);
            
            var itemConfig = ServerContext.ItemConfig;
            
            foreach (var item in itemConfig.ItemConfigDataList)
            {
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(item.ItemId);
                
                // アイテムリストを設定
                // Set the item list
                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                itemSlotObject.SetItem(itemViewData, 0);
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                _itemListObjects.Add(itemSlotObject);
                
                // ハイライトオブジェクトを設定
                // Set the highlight object
                var target = itemSlotObject.gameObject.AddComponent<UIHighlightTutorialTargetObject>();
                target.Initialize("itemRecipeList:" + item.Name);
            }
            
            nextRecipeButton.onClick.AddListener(() =>
            {
                _currentCraftingConfigIndex++;
                if (_currentCraftingConfigInfos.Count <= _currentCraftingConfigIndex) _currentCraftingConfigIndex = 0;
                DisplayRecipe(_currentCraftingConfigIndex);
            });
            
            prevRecipeButton.onClick.AddListener(() =>
            {
                _currentCraftingConfigIndex--;
                if (_currentCraftingConfigIndex < 0) _currentCraftingConfigIndex = _currentCraftingConfigInfos.Count - 1;
                DisplayRecipe(_currentCraftingConfigIndex);
            });
            
            craftButton.OnCraftFinish.Subscribe(_ =>
            {
                if (_currentCraftingConfigInfos?.Count == 0) return;
                ClientContext.VanillaApi.SendOnly.Craft(_currentCraftingConfigInfos[_currentCraftingConfigIndex].RecipeId);
            }).AddTo(this);
        }
        
        private void OnClickItemList(ItemSlotObject slot)
        {
            var craftConfig = ServerContext.CraftingConfig;
            _currentCraftingConfigInfos = craftConfig.GetResultItemCraftingConfigList(slot.ItemViewData.ItemId);
            if (_currentCraftingConfigInfos.Count == 0) return;
            
            _currentCraftingConfigIndex = 0;
            DisplayRecipe(0);
        }
        
        private void OnItemChange(int slot)
        {
            var enableItem = IsAllItemCraftable();
            foreach (var itemUI in _itemListObjects)
            {
                var isGrayOut = !enableItem.Contains(itemUI.ItemViewData.ItemId);
                itemUI.SetGrayOut(isGrayOut);
            }
        }
        
        
        private void DisplayRecipe(int index)
        {
            var craftingConfigInfo = _currentCraftingConfigInfos[index];
            
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
                foreach (var requiredItem in craftingConfigInfo.CraftRequiredItemInfos)
                {
                    var item = requiredItem.ItemStack;
                    var itemViewData = ClientContext.ItemImageContainer.GetItemView(item.Id);
                    
                    var itemSlotObject = Instantiate(itemSlotObjectPrefab, craftMaterialParent);
                    itemSlotObject.SetItem(itemViewData, item.Count);
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                    _craftMaterialSlotList.Add(itemSlotObject);
                }
            }
            
            void SetResultSlot()
            {
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(craftingConfigInfo.ResultItem.Id);
                _craftResultSlot = Instantiate(itemSlotObjectPrefab, craftResultParent);
                _craftResultSlot.SetItem(itemViewData, craftingConfigInfo.ResultItem.Count);
            }
            
            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = _currentCraftingConfigInfos.Count != 1;
                nextRecipeButton.interactable = _currentCraftingConfigInfos.Count != 1;
                recipeCountText.text = $"{_currentCraftingConfigIndex + 1} / {_currentCraftingConfigInfos.Count}";
                craftButton.SetInteractable(IsCraftable(craftingConfigInfo));
                
                var itemName = ServerContext.ItemConfig.GetItemConfig(craftingConfigInfo.ResultItem.Id).Name;
                itemNameText.text = itemName;
            }
            
            #endregion
        }
        
        
        /// <summary>
        ///     そのレシピがクラフト可能かどうかを返す
        ///     この処理はある1つのレシピに対してのみ使い、一気にすべてのアイテムがクラフト可能かチェックするには<see cref="IsAllItemCraftable" />を用いる
        /// </summary>
        private bool IsCraftable(CraftingConfigInfo craftingConfigInfo)
        {
            var itemPerCount = new Dictionary<int, int>();
            foreach (var item in _localPlayerInventory)
            {
                if (item.Id == ItemConst.EmptyItemId) continue;
                if (itemPerCount.ContainsKey(item.Id))
                    itemPerCount[item.Id] += item.Count;
                else
                    itemPerCount.Add(item.Id, item.Count);
            }
            
            foreach (var requiredItem in craftingConfigInfo.CraftRequiredItemInfos)
            {
                var item = requiredItem.ItemStack;
                
                if (!itemPerCount.ContainsKey(item.Id)) return false;
                if (itemPerCount[item.Id] < item.Count) return false;
            }
            
            return true;
        }
        
        
        private HashSet<int> IsAllItemCraftable()
        {
            var itemPerCount = new Dictionary<int, int>();
            foreach (var item in _localPlayerInventory)
            {
                if (item.Id == ItemConst.EmptyItemId) continue;
                if (itemPerCount.ContainsKey(item.Id))
                    itemPerCount[item.Id] += item.Count;
                else
                    itemPerCount.Add(item.Id, item.Count);
            }
            
            var result = new HashSet<int>();
            
            var craftingConfig = ServerContext.CraftingConfig;
            foreach (var configData in craftingConfig.CraftingConfigList)
            {
                if (result.Contains(configData.ResultItem.Id)) continue; //すでにクラフト可能なアイテムならスキップ
                var isCraftable = true;
                foreach (var requiredItem in configData.CraftRequiredItemInfos)
                {
                    var item = requiredItem.ItemStack;
                    if (!itemPerCount.ContainsKey(item.Id) || itemPerCount[item.Id] < item.Count)
                    {
                        isCraftable = false;
                        break;
                    }
                }
                
                if (isCraftable) result.Add(configData.ResultItem.Id);
            }
            
            return result;
        }
        
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}