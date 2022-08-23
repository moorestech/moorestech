using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using MainGame.Basic;
using MainGame.UnityView.Block;
using MainGame.UnityView.UI.Builder;
using MainGame.UnityView.UI.Builder.Unity;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using TMPro;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class ItemRecipeView : MonoBehaviour
    {
        private ItemImages _itemImages;
        private BlockObjects _blockObjects;
        
        [SerializeField] private GameObject craftingRecipeView;
        [SerializeField] private GameObject machineCraftingRecipeView;
        
        [SerializeField] private List<InventoryItemSlot> craftingRecipeSlots;
        [SerializeField] private InventoryItemSlot craftingResultSlot;
        
        [SerializeField] private List<InventoryItemSlot> machineCraftingRecipeSlots;
        [SerializeField] private InventoryItemSlot machineCraftingResultSlot;
        [SerializeField] private TMP_Text machineNameText;
        
        public event CraftRecipeItemListViewer.ItemSlotClick OnCraftSlotClick;

        public event Action<ItemStack> OnCursorEnter;
        public event Action<ItemStack> OnCursorExit;
        
        [Inject]
        public void Construct(ItemImages itemImages,BlockObjects blockObjects)
        {
            _blockObjects = blockObjects;
            _itemImages = itemImages;
            foreach (var slot in craftingRecipeSlots)
            {
                slot.OnLeftClickDown += OnClick;
                slot.OnCursorEnter += InvokeCursorEnter;
                slot.OnCursorExit += InvokeCursorExit;
            }
            foreach (var slot in machineCraftingRecipeSlots)
            {
                slot.OnLeftClickDown += OnClick;
                slot.OnCursorEnter += InvokeCursorEnter;
                slot.OnCursorExit += InvokeCursorExit;
            }
            craftingResultSlot.OnCursorEnter += _ => OnCursorEnter?.Invoke(_craftResultItemStack);
            craftingResultSlot.OnCursorExit += _ => OnCursorExit?.Invoke(_craftResultItemStack);
            machineCraftingResultSlot.OnCursorEnter += _ => OnCursorEnter?.Invoke(_machineCraftResultItemStack);
            machineCraftingResultSlot.OnCursorExit += _ => OnCursorExit?.Invoke(_machineCraftResultItemStack);
        }
        
        
        private List<ItemStack> _craftItemStacks = new();
        private ItemStack _craftResultItemStack;
        
        public void SetCraftRecipe(List<ItemStack> itemStacks,ItemStack result)
        {
            craftingRecipeView.SetActive(true);
            machineCraftingRecipeView.SetActive(false);

            for (int i = 0; i < craftingRecipeSlots.Count; i++)
            {
                var item = itemStacks[i];
                craftingRecipeSlots[i].SetItem(_itemImages.GetItemView(item.ID),item.Count);
            }
            craftingResultSlot.SetItem(_itemImages.GetItemView(result.ID),result.Count);
            _craftResultItemStack = result;
            
            _craftItemStacks = itemStacks;
        }
        
        private List<ItemStack> _machineCraftItemStacks = new();
        private ItemStack _machineCraftResultItemStack;
        
        public void SetMachineCraftRecipe(List<ItemStack> itemStacks,ItemStack result,int blockId)
        {
            craftingRecipeView.SetActive(false);
            machineCraftingRecipeView.SetActive(true);

            machineNameText.text = _blockObjects.GetName(blockId);
            
            for (int i = 0; i < machineCraftingRecipeSlots.Count; i++)
            {
                if (itemStacks.Count <= i)
                {
                    machineCraftingRecipeSlots[i].gameObject.SetActive(false);
                    continue;
                }
                
                machineCraftingRecipeSlots[i].gameObject.SetActive(true);
                var item = itemStacks[i];
                machineCraftingRecipeSlots[i].SetItem(_itemImages.GetItemView(item.ID),item.Count);
            }
            _machineCraftResultItemStack = result;
            machineCraftingResultSlot.SetItem(_itemImages.GetItemView(result.ID),result.Count);
            _machineCraftItemStacks = itemStacks;
        }

        private void OnClick(InventoryItemSlot inventoryItemSlot) { OnCraftSlotClick?.Invoke(GetItemStack(inventoryItemSlot).ID); }
        private void InvokeCursorEnter(InventoryItemSlot inventoryItemSlot) { OnCursorEnter?.Invoke(GetItemStack(inventoryItemSlot)); }
        private void InvokeCursorExit(InventoryItemSlot inventoryItemSlot) { OnCursorExit?.Invoke(GetItemStack(inventoryItemSlot)); }


        private ItemStack GetItemStack(InventoryItemSlot inventoryItemSlot)
        {
            var craftIndex = craftingRecipeSlots.IndexOf(inventoryItemSlot);
            var machineCraftIndex = machineCraftingRecipeSlots.IndexOf(inventoryItemSlot);
            
            if (craftIndex != -1)
            {
                return _craftItemStacks[craftIndex];
            }
            else if (machineCraftIndex != -1)
            {
                return _machineCraftItemStacks[machineCraftIndex];
            }

            return new ItemStack();
        }
    }
}