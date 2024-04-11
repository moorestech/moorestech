using System.Collections.Generic;
using System.Linq;
using Client.Game.Context;
using Client.Game.UI.Inventory.Element;
using Client.Game.UI.Inventory.Main;
using Core.Const;
using Core.Item.Interface;
using Game.Block.Interface.RecipeConfig;
using Game.Context;
using Game.Crafting.Config;
using Game.Crafting.Interface;
using TMPro;
using UniRx;
using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Client.Game.UI.Inventory.Sub
{
    public class CraftInventoryView : MonoBehaviour
    {
        private ILocalPlayerInventory _localPlayerInventory;

        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;

        [SerializeField] private RectTransform craftMaterialParent;
        [SerializeField] private RectTransform craftResultParent;

        [SerializeField] private RectTransform itemListParent;

        [SerializeField] private CraftButton _craftButton;
        [SerializeField] private Button nextRecipeButton;
        [SerializeField] private Button prevRecipeButton;
        [SerializeField] private TMP_Text recipeCountText;

        [SerializeField] private ItemSlotObject _requiredMachineSlot;

        private readonly List<ItemSlotObject> _itemListObjects = new();

        private int _currentItem = 0;

        private List<CraftingConfigData> _currentCraftingConfigDataList = new();
        private List<MachineRecipeData> _currentMachineRecipeDataList = new();
        private int _currentCraftingConfigIndex;

        private int CraftRecipesAmount => _currentCraftingConfigDataList.Count;
        private int MachineRecipesAmount => _currentMachineRecipeDataList.Count;
        private int TotalRecipeAmount => CraftRecipesAmount + MachineRecipesAmount;
        public int CurrentCraftingRecipeIndex { get => _currentCraftingConfigIndex; set => _currentCraftingConfigIndex = (value < 0) ? (TotalRecipeAmount - 1) : (value % TotalRecipeAmount); }
        private bool IsCraftRecipeSelected => CurrentCraftingRecipeIndex < CraftRecipesAmount;
        private bool IsMachineRecipeSelected => CurrentCraftingRecipeIndex >= CraftRecipesAmount;
        private int CraftRecipeIndex => CurrentCraftingRecipeIndex;
        private int MachineRecipeIndex => CurrentCraftingRecipeIndex - CraftRecipesAmount;

        [Inject]
        public void Construct(ILocalPlayerInventory localPlayerInventory)
        {
            _localPlayerInventory = localPlayerInventory;
            _localPlayerInventory.OnItemChange.Subscribe(OnItemChange);

            var itemConfig = ServerContext.ItemConfig;

            foreach (var item in itemConfig.ItemConfigDataList)
            {
                var itemViewData = MoorestechContext.ItemImageContainer.GetItemView(item.ItemId);

                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                itemSlotObject.SetItem(itemViewData, 0);
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemSlot);
                _itemListObjects.Add(itemSlotObject);
            }

            nextRecipeButton.onClick.AddListener(() =>
            {
                CurrentCraftingRecipeIndex++;
                DisplayRecipe();
            });

            prevRecipeButton.onClick.AddListener(() =>
            {
                CurrentCraftingRecipeIndex--;
                DisplayRecipe();
            });

            _craftButton.OnCraftFinish.Subscribe(_ =>
            {
                if (IsCraftRecipeSelected)
                    MoorestechContext.VanillaApi.SendOnly.Craft(_currentCraftingConfigDataList[_currentCraftingConfigIndex].RecipeId);
            }
            ).AddTo(this);
        }

        private void OnClickItemSlot(ItemSlotObject slot)
        {
            if (slot.ItemViewData.ItemId == _currentItem)
                return;
            _currentItem = slot.ItemViewData.ItemId;

            Clear();

            // collect all normal recipes that have the item as a result
            CollectCraftingRecipes(slot.ItemViewData.ItemId);

            // collect all the machine recipes that have the item as a result
            CollectMachineRecipes(slot.ItemViewData.ItemId);

            // if machine, collect all the recipes that use the machine
            if (ServerContext.BlockConfig.IsBlock(slot.ItemViewData.ItemId))
                CollectRecipesUsingMachines(ServerContext.BlockConfig.ItemIdToBlockId(slot.ItemViewData.ItemId));

            if (TotalRecipeAmount != 0)
                DisplayRecipe();
        }

        private void OnItemChange(int slot)
        {
            HashSet<int> enableItem = IsAllItemCraftable();
            foreach (var itemUI in _itemListObjects)
            {
                var isGrayOut = enableItem.Contains(itemUI.ItemViewData.ItemId);
                itemUI.SetGrayOut(isGrayOut);
            }
        }

        private void CollectCraftingRecipes(int itemId)
        {
            var craftConfig = ServerContext.CraftingConfig;
            var results = craftConfig.GetResultItemCraftingConfigList(itemId);

            if (results?.Count != 0)
                _currentCraftingConfigDataList = results.ToList();
        }

        private void CollectMachineRecipes(int itemId)
        {
            var machineRecipeConfig = ServerContext.MachineRecipeConfig;
            var machineResults = machineRecipeConfig
                .GetAllRecipeData()
                .Where(recipe => recipe.ItemOutputs.Any(i => i.OutputItem.Id == itemId));

            _currentMachineRecipeDataList.AddRange(machineResults);
        }

        private void CollectRecipesUsingMachines(int blockId)
        {
            var machineRecipeConfig = ServerContext.MachineRecipeConfig;
            var machineResults = machineRecipeConfig
                .GetAllRecipeData()
                .Where(recipe => recipe.BlockId == blockId);

            _currentMachineRecipeDataList.AddRange(machineResults);
        }

        private void Clear()
        {
            _currentCraftingConfigIndex = 0;
            _currentCraftingConfigDataList.Clear();
            _currentMachineRecipeDataList.Clear();

            recipeCountText.text = "0 / 0";

            _requiredMachineSlot.SetActive(false);

            ClearSlotObject();
        }

        private void DisplayRecipe()
        {
            if (IsCraftRecipeSelected)
            {
                DisplayCraftRecipe(CraftRecipeIndex);
                return;
            }

            if (IsMachineRecipeSelected)
            {
                DisplayMachineRecipe(MachineRecipeIndex);
                return;
            }
        }

        private void DisplayCraftRecipe(int index)
        {
            CraftingConfigData craftingConfigData = _currentCraftingConfigDataList[index];

            bool isCraftable = IsCraftable(craftingConfigData);

            ClearSlotObject();
            SetMaterialSlot(craftingConfigData.CraftItems);
            SetResultSlot(craftingConfigData.ResultItem);
            UpdateButtonAndText(isCraftable, false);
        }

        private void DisplayMachineRecipe(int index)
        {
            MachineRecipeData recipeData = _currentMachineRecipeDataList[index];

            ClearSlotObject();
            SetMaterialSlot(recipeData.ItemInputs);
            SetResultSlots(recipeData.ItemOutputs.Select(x => x.OutputItem));
            UpdateButtonAndText(false, true);

            SetRequiredMachine(recipeData.BlockId);
        }
        #region InternalMethod

        private void ClearSlotObject()
        {
            foreach (Transform child in craftResultParent)
            {
                Destroy(child.gameObject);
            }
            foreach (Transform child in craftMaterialParent)
            {
                Destroy(child.gameObject);
            }
        }

        private void SetMaterialSlot(IEnumerable<IItemStack> items)
        {
            foreach (var item in items)
            {
                AddItemSlot(item, craftMaterialParent);
            }
        }

        private void SetMaterialSlot(IItemStack items)
        {
            AddItemSlot(items, craftMaterialParent);
        }

        private void SetResultSlots(IEnumerable<IItemStack> items)
        {
            foreach (var item in items)
            {
                AddItemSlot(item, craftResultParent);
            }
        }

        private void SetResultSlot(IItemStack item)
        {
            AddItemSlot(item, craftResultParent);
        }

        private void AddItemSlot(IItemStack item, RectTransform parent)
        {
            var itemSlotObject = Instantiate(itemSlotObjectPrefab, parent);

            var itemViewData = MoorestechContext.ItemImageContainer.GetItemView(item.Id);
            itemSlotObject.SetItem(itemViewData, item.Count);

            itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemSlot);
        }

        

        private void UpdateButtonAndText(bool isCraftable, bool isMachineRecipe)
        {
            int recipesAmount = TotalRecipeAmount;

            prevRecipeButton.interactable = recipesAmount > 1;
            nextRecipeButton.interactable = recipesAmount > 1;
            
            recipeCountText.text = $"{CurrentCraftingRecipeIndex + 1} / {TotalRecipeAmount}";
            
            _craftButton.UpdateInteractable(isCraftable);
            _craftButton.gameObject.SetActive(!isMachineRecipe);

            _requiredMachineSlot.SetActive(isMachineRecipe);
        }

        private void SetRequiredMachine(int blockId)
        {
            int itemID = ServerContext.BlockConfig.BlockIdToItemId(blockId);
            var itemViewData = MoorestechContext.ItemImageContainer.GetItemView(itemID);
            _requiredMachineSlot.SetItem(itemViewData, 0);

            _requiredMachineSlot.SetActive(true);

            _requiredMachineSlot.OnLeftClickUp.Subscribe(slot =>
            {
                OnClickItemSlot(slot);
            });
        }

        #endregion


        /// <summary>
        ///     そのレシピがクラフト可能かどうかを返す
        ///     この処理はある1つのレシピに対してのみ使い、一気にすべてのアイテムがクラフト可能かチェックするには<see cref="IsAllItemCraftable" />を用いる
        /// </summary>
        private bool IsCraftable(CraftingConfigData craftingConfigData)
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
                foreach (var material in configData.CraftItems)
                    if (!itemPerCount.ContainsKey(material.Id) || itemPerCount[material.Id] < material.Count)
                    {
                        isCraftable = false;
                        break;
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