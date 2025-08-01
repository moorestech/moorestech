using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.Sub;
using Core.Master;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.RecipeViewer
{
    public class MachineRecipeView : MonoBehaviour
    {
        [SerializeField] private RectTransform inputParent;
        [SerializeField] private RectTransform outputParent;
        
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text recipeCountText;
        [SerializeField] private ItemSlotView machineView;
        
        public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        
        private readonly List<ItemSlotView> _inputSlotList = new();
        private readonly List<ItemSlotView> _outputSlotList = new();
        [Inject] private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        private int MachineRecipeCount => _currentItemRecipes.MachineRecipes[_currentBlockId].Count;
        private RecipeViewerItemRecipes _currentItemRecipes;
        private BlockId _currentBlockId;
        private int _currentIndex;
        
        [Inject]
        public void Construct()
        {
            machineView.SetFrameType(ItemSlotFrameType.MachineSlot);
            machineView.OnLeftClickUp.Subscribe(OnClickMaterialItem);
            
            nextRecipeButton.onClick.AddListener(() =>
            {
                _currentIndex++;
                if (MachineRecipeCount <= _currentIndex) _currentIndex = 0;
                DisplayRecipe(_currentIndex);
            });
            
            prevRecipeButton.onClick.AddListener(() =>
            {
                _currentIndex--;
                if (_currentIndex < 0) _currentIndex = MachineRecipeCount - 1;
                DisplayRecipe(_currentIndex);
            });
        }
        
        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes)
        {
            _currentItemRecipes = recipeViewerItemRecipes;
            _currentIndex = 0;
            if (recipeViewerItemRecipes.MachineRecipes.Count != 0)
            {
                _currentBlockId = recipeViewerItemRecipes.MachineRecipes.First().Key;
            }
        }
        
        public void SetBlockId(BlockId blockId)
        {
            _currentBlockId = blockId;
            _currentIndex = 0;
            DisplayRecipe(_currentIndex);
        }
        
        public void DisplayRecipe(int index)
        {
            var machineRecipes = _currentItemRecipes.MachineRecipes[_currentBlockId][index];
            
            ClearSlotObject();
            
            SetInputSlot();
            SetOutputSlot();
            SetMachineSlot();
            
            UpdateButtonAndText();
            
            #region InternalMethod
            
            void ClearSlotObject()
            {
                foreach (var materialSlot in _inputSlotList) Destroy(materialSlot.gameObject);
                _inputSlotList.Clear();
                foreach (var resultSlot in _outputSlotList) Destroy(resultSlot.gameObject);
                _outputSlotList.Clear();
            }
            
            void SetInputSlot()
            {
                foreach (var requiredItem in machineRecipes.InputItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                    
                    var itemSlotObject = Instantiate(ItemSlotView.Prefab, inputParent);
                    itemSlotObject.SetItem(itemViewData, requiredItem.Count);
                    _inputSlotList.Add(itemSlotObject);
                    
                    // 原材料をクリックしたときにそのレシピを表示するようにする
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickMaterialItem);
                }
            }
            
            void SetOutputSlot()
            {
                foreach (var requiredItem in machineRecipes.OutputItems)
                {
                    var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                    var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                    
                    var itemSlotObject = Instantiate(ItemSlotView.Prefab, outputParent);
                    itemSlotObject.SetItem(itemViewData, requiredItem.Count);
                    _outputSlotList.Add(itemSlotObject);
                    
                    // 原材料をクリックしたときにそのレシピを表示するようにする
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickMaterialItem);
                }
            }
            
            void SetMachineSlot()
            {
                var blockItemId = MasterHolder.BlockMaster.GetItemId(_currentBlockId);
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(blockItemId);
                machineView.SetItem(itemViewData, 0);
            }
            
            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = MachineRecipeCount != 1;
                nextRecipeButton.interactable = MachineRecipeCount != 1;
                recipeCountText.text = $"{_currentIndex + 1} / {MachineRecipeCount}";
                
                var itemName = MasterHolder.ItemMaster.GetItemMaster(_currentItemRecipes.ResultItemId).Name;
                itemNameText.text = itemName;
            }
            
            #endregion
        }
        
        private void OnClickMaterialItem(ItemSlotView itemSlotView)
        {
            var itemId = itemSlotView.ItemViewData.ItemId;
            var itemRecipes = _itemRecipeViewerDataContainer.GetItem(itemId);
            _onClickItem.OnNext(itemRecipes);
        }
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}