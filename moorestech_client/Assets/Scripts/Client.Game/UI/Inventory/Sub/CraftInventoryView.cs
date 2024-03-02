using System.Collections.Generic;
using Client.Game.Context;
using Client.Network.API;
using Core.Const;
using Core.Item.Config;
using Game.Crafting.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.Main;
using ServerServiceProvider;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Sub
{
    public class CraftInventoryView : MonoBehaviour
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform craftMaterialParent;
        private readonly List<ItemSlotObject> _craftMaterialSlotList = new();
        [SerializeField] private RectTransform craftResultParent;
        private ItemSlotObject _craftResultSlot;
        
        [SerializeField] private RectTransform itemListParent;
        private readonly List<ItemSlotObject> _itemListObjects = new();

        [SerializeField] private Button craftButton;
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        [SerializeField] private TMP_Text recipeCountText;
        
        private IItemConfig _itemConfig;
        private ICraftingConfig _craftingConfig;
        private ILocalPlayerInventory _localPlayerInventory;
        
        private IReadOnlyList<CraftingConfigData> _currentCraftingConfigDataList;
        private int _currentCraftingConfigIndex;
        



        [Inject]
        public void Construct(MoorestechServerServiceProvider singlePlay,ILocalPlayerInventory localPlayerInventory)
        {
            _itemConfig = singlePlay.ItemConfig;
            _craftingConfig = singlePlay.CraftingConfig;
            _localPlayerInventory = localPlayerInventory;
            _localPlayerInventory.OnItemChange.Subscribe(OnItemChange);

            foreach (var item in _itemConfig.ItemConfigDataList)
            {
                var itemViewData = MoorestechContext.ItemImageContainer.GetItemView(item.ItemId);

                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                itemSlotObject.SetItem(itemViewData, 0);
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                _itemListObjects.Add(itemSlotObject);
            }
            
            craftButton.onClick.AddListener(() =>
            {
                if (_currentCraftingConfigDataList.Count == 0) return;
                MoorestechContext.VanillaApi.SendOnly.Craft(_currentCraftingConfigDataList[_currentCraftingConfigIndex].RecipeId);
            });

            nextRecipeButton.onClick.AddListener(() =>
            {
                _currentCraftingConfigIndex++;
                if (_currentCraftingConfigDataList.Count <= _currentCraftingConfigIndex) _currentCraftingConfigIndex = 0;
                DisplayRecipe(_currentCraftingConfigIndex);
            });
            prevRecipeButton.onClick.AddListener(() =>
            {
                _currentCraftingConfigIndex--;
                if (_currentCraftingConfigIndex < 0) _currentCraftingConfigIndex = _currentCraftingConfigDataList.Count - 1;
                DisplayRecipe(_currentCraftingConfigIndex);
            });
            
        }

        private void OnClickItemList(ItemSlotObject slot)
        {
            _currentCraftingConfigDataList = _craftingConfig.GetResultItemCraftingConfigList(slot.ItemViewData.ItemId);
            if (_currentCraftingConfigDataList.Count == 0) return;
            
            _currentCraftingConfigIndex = 0;
            DisplayRecipe(0);
        }

        private void OnItemChange(int slot)
        {
            var enableItem = IsAllItemCraftable();
            foreach (var itemUI in _itemListObjects)
            {
                var isGrayOut = enableItem.Contains(itemUI.ItemViewData.ItemId);
                itemUI.SetGrayOut(isGrayOut);
            }
        }
        

        private void DisplayRecipe(int index)
        {
            var craftingConfigData = _currentCraftingConfigDataList[index];
            
            ClearSlotObject();

            SetMaterialSlot();
            
            SetResultSlot();

            UpdateButtonAndText();
            
            #region InternalMethod

            void ClearSlotObject()
            {
                foreach (var materialSlot in _craftMaterialSlotList)
                {
                    Destroy(materialSlot.gameObject);
                }
                _craftMaterialSlotList.Clear();
                if (_craftResultSlot != null)
                {
                    Destroy(_craftResultSlot.gameObject);
                }
            }

            void SetMaterialSlot()
            {
                foreach (var material in craftingConfigData.CraftItems)
                {
                    var itemViewData = MoorestechContext.ItemImageContainer.GetItemView(material.Id);
                    var itemSlotObject = Instantiate(itemSlotObjectPrefab, craftMaterialParent);
                    itemSlotObject.SetItem(itemViewData, material.Count);
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                    _craftMaterialSlotList.Add(itemSlotObject);
                }
            }
            
            void SetResultSlot()
            {
                var itemViewData = MoorestechContext.ItemImageContainer.GetItemView(craftingConfigData.ResultItem.Id);
                _craftResultSlot = Instantiate(itemSlotObjectPrefab, craftResultParent);
                _craftResultSlot.SetItem(itemViewData, craftingConfigData.ResultItem.Count);
            }

            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = _currentCraftingConfigDataList.Count != 1;
                nextRecipeButton.interactable = _currentCraftingConfigDataList.Count != 1;
                recipeCountText.text = $"{_currentCraftingConfigIndex + 1} / {_currentCraftingConfigDataList.Count}";
                craftButton.interactable = IsCraftable(craftingConfigData);
            }
            

            #endregion
        }


        /// <summary>
        /// そのレシピがクラフト可能かどうかを返す
        /// この処理はある1つのレシピに対してのみ使い、一気にすべてのアイテムがクラフト可能かチェックするには<see cref="IsAllItemCraftable"/>を用いる
        /// </summary>
        private bool IsCraftable(CraftingConfigData craftingConfigData)
        {
            var itemPerCount = new Dictionary<int, int>();
            foreach (var item in _localPlayerInventory)
            {
                if (item.Id == ItemConst.EmptyItemId) continue;
                if (itemPerCount.ContainsKey(item.Id))
                {
                    itemPerCount[item.Id] += item.Count;
                }
                else
                {
                    itemPerCount.Add(item.Id, item.Count);
                }
            }
            
            foreach (var material in craftingConfigData.CraftItems)
            {
                if (!itemPerCount.ContainsKey(material.Id)) return false;
                if (itemPerCount[material.Id] < material.Count) return false;
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
                {
                    itemPerCount[item.Id] += item.Count;
                }
                else
                {
                    itemPerCount.Add(item.Id, item.Count);
                }
            }

            var result = new HashSet<int>();

            foreach (var configData in _craftingConfig.CraftingConfigList)
            {
                if (result.Contains(configData.ResultItem.Id)) continue; //すでにクラフト可能なアイテムならスキップ
                var isCraftable = true;
                foreach (var material in configData.CraftItems)
                {
                    if (!itemPerCount.ContainsKey(material.Id) || itemPerCount[material.Id] < material.Count)
                    {
                        isCraftable = false;
                        break;
                    }
                }

                if (isCraftable)
                {
                    result.Add(configData.ResultItem.Id);
                }
            }
            
            return result;
        }




        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
    }
}