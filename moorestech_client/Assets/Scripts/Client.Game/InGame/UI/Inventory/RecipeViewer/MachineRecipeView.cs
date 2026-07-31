// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Localization;
using Core.Master;
using Mooresmaster.Model.MachineRecipesModule;
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
        
        private int MachineRecipeCount => _currentUnlockedMachineRecipes[_currentBlockId].Count;
        private RecipeViewerItemRecipes _currentItemRecipes;
        private Dictionary<BlockId, List<MachineRecipeMasterElement>> _currentUnlockedMachineRecipes = new();
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

            Localize.OnLanguageChanged
                .Subscribe(_ => RefreshLocalizedTexts())
                .AddTo(this);

            #region Internal

            void RefreshLocalizedTexts()
            {
                if (_currentItemRecipes == null ||
                    !_currentUnlockedMachineRecipes.ContainsKey(_currentBlockId)) return;
                RefreshItemName();
                RefreshMachineSlot();
            }

            #endregion
        }
        
        public void SetRecipes(RecipeViewerItemRecipes recipeViewerItemRecipes, Dictionary<BlockId, List<MachineRecipeMasterElement>> unlockedMachineRecipes)
        {
            _currentItemRecipes = recipeViewerItemRecipes;
            _currentUnlockedMachineRecipes = unlockedMachineRecipes;
            _currentIndex = 0;
            if (_currentUnlockedMachineRecipes.Count != 0)
            {
                _currentBlockId = _currentUnlockedMachineRecipes.First().Key;
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
            var machineRecipes = _currentUnlockedMachineRecipes[_currentBlockId][index];
            
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
                RefreshMachineSlot();
            }
            
            void UpdateButtonAndText()
            {
                prevRecipeButton.interactable = MachineRecipeCount != 1;
                nextRecipeButton.interactable = MachineRecipeCount != 1;
                recipeCountText.text = $"{_currentIndex + 1} / {MachineRecipeCount}";
                
                RefreshItemName();
            }
            
            #endregion
        }

        private void RefreshItemName()
        {
            var itemMaster = MasterHolder.ItemMaster.GetItemMaster(_currentItemRecipes.ResultItemId);
            itemNameText.text = Localize.GetContent(
                ContentLocalizationKeys.ItemName(itemMaster.ItemGuid));
        }

        private void RefreshMachineSlot()
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(_currentBlockId);
            var itemViewData = ClientContext.BlockImageContainer.GetBlockView(_currentBlockId);
            var toolTip = Localize.GetContent(ContentLocalizationKeys.BlockName(blockMaster.BlockGuid));
            machineView.SetItem(itemViewData, 0, toolTip);
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
