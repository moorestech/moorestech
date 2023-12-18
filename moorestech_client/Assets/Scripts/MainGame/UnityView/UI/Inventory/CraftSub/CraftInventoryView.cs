using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item.Config;
using Game.Crafting.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.Main;
using MainGame.UnityView.UI.UIObjects;
using Server.Protocol.PacketResponse;
using SinglePlay;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.CraftSub
{
    public class CraftInventoryView : MonoBehaviour
    {
        [SerializeField] private UIBuilderItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform craftMaterialParent;
        private readonly List<UIBuilderItemSlotObject> _craftMaterialSlotList = new();
        [SerializeField] private RectTransform craftResultParent;
        private UIBuilderItemSlotObject _craftResultSlot;
        
        [SerializeField] private RectTransform itemListParent;
        
        [SerializeField] private Button craftButton;
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        [SerializeField] private TMP_Text recipeCountText;
        
        private IItemConfig _itemConfig;
        private ICraftingConfig _craftingConfig;
        private ItemImageContainer _itemImageContainer;
        private InventoryMainAndSubCombineItems _inventoryItems;
        
        private IReadOnlyList<CraftingConfigData> _currentCraftingConfigDataList;
        private int _currentCraftingConfigIndex;
        



        [Inject]
        public void Construct(SinglePlayInterface singlePlay,ItemImageContainer itemImageContainer,InventoryMainAndSubCombineItems inventoryMainAndSubCombineItems)
        {
            _itemConfig = singlePlay.ItemConfig;
            _craftingConfig = singlePlay.CraftingConfig;
            _itemImageContainer = itemImageContainer;
            _inventoryItems = inventoryMainAndSubCombineItems;

            foreach (var item in _itemConfig.ItemConfigDataList)
            {
                var itemViewData = _itemImageContainer.GetItemView(item.ItemId);
                
                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                itemSlotObject.SetItem(itemViewData, 0,false);
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
            }
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

        private void OnClickItemList(UIBuilderItemSlotObject slot)
        {
            _currentCraftingConfigDataList = _craftingConfig.GetResultItemCraftingConfigList(slot.ItemViewData.ItemId);
            if (_currentCraftingConfigDataList.Count == 0) return;
            
            _currentCraftingConfigIndex = 0;
            DisplayRecipe(0);
        }

        private void DisplayRecipe(int index)
        {
            var craftingConfigData = _craftingConfig.GetCraftingConfigData(index);
            
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
                    var itemViewData = _itemImageContainer.GetItemView(material.Id);
                    var itemSlotObject = Instantiate(itemSlotObjectPrefab, craftMaterialParent);
                    itemSlotObject.SetItem(itemViewData, material.Count, true);
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                    _craftMaterialSlotList.Add(itemSlotObject);
                }
            }
            
            void SetResultSlot()
            {
                var itemViewData = _itemImageContainer.GetItemView(craftingConfigData.ResultItem.Id);
                _craftResultSlot = Instantiate(itemSlotObjectPrefab, craftResultParent);
                _craftResultSlot.SetItem(itemViewData, craftingConfigData.ResultItem.Count, true);
            }

            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = _currentCraftingConfigDataList.Count != 1;
                nextRecipeButton.interactable = _currentCraftingConfigDataList.Count != 1;
                recipeCountText.text = $"{_currentCraftingConfigIndex + 1}/{_currentCraftingConfigDataList.Count}";
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
            foreach (var item in _inventoryItems)
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


        private Dictionary<int, bool> IsAllItemCraftable()
        {
            throw new NotImplementedException();
        }




        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
        
    }
}