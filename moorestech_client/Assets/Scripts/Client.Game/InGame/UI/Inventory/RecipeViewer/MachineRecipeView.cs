using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Element;
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
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform inputParent;
        [SerializeField] private RectTransform outputParent;
        
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text recipeCountText;
        [SerializeField] private ItemSlotObject machineObject;
        
        public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        
        private readonly List<ItemSlotObject> _inputSlotList = new();
        private readonly List<ItemSlotObject> _outputSlotList = new();
        private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        
        private int MachineRecipeCount => _currentItemRecipes.MachineRecipes[_currentBlockId].Count;
        private RecipeViewerItemRecipes _currentItemRecipes;
        private BlockId _currentBlockId;
        private int _currentIndex;
        

        [Inject]
        public void Construct(ItemRecipeViewerDataContainer itemRecipeViewerDataContainer)
        {
            machineObject.SetFrame(ItemSlotFrameType.MachineSlot);
            _itemRecipeViewerDataContainer = itemRecipeViewerDataContainer;
            
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
        }
        
        public void SetBlockId(BlockId blockId)
        {
            _currentBlockId = blockId;
            _currentIndex = 0;
            DisplayRecipe(_currentIndex);
        }
        
        private void DisplayRecipe(int index)
        {
            var machineRecipes = _currentItemRecipes.MachineRecipes[_currentBlockId][index];
            
            ClearSlotObject();
            
            SetInputSlot();
            
            SetOutputSlot();
            
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
                    
                    var itemSlotObject = Instantiate(itemSlotObjectPrefab, inputParent);
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
                    
                    var itemSlotObject = Instantiate(itemSlotObjectPrefab, inputParent);
                    itemSlotObject.SetItem(itemViewData, requiredItem.Count);
                    _outputSlotList.Add(itemSlotObject);
                    
                    // 原材料をクリックしたときにそのレシピを表示するようにする
                    itemSlotObject.OnLeftClickUp.Subscribe(OnClickMaterialItem);
                }
            }
            
            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = MachineRecipeCount != 1;
                nextRecipeButton.interactable = MachineRecipeCount != 1;
                recipeCountText.text = $"{_currentIndex + 1} / {MachineRecipeCount}";
                
                var itemName = MasterHolder.ItemMaster.GetItemMaster(_currentItemRecipes.ResultItemId).Name;
                itemNameText.text = itemName;
            }
            
            void OnClickMaterialItem(ItemSlotObject itemSlotObject)
            {
                var itemId = itemSlotObject.ItemViewData.ItemId;
                var itemRecipes = _itemRecipeViewerDataContainer.CraftRecipeViewerElements[itemId];
                _onClickItem.OnNext(itemRecipes);
            }
            
            #endregion
        }
        
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
}