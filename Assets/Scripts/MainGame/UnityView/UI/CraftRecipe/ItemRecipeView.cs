using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.Block;
using MainGame.UnityView.UI.Builder.Unity;
using MainGame.UnityView.UI.Inventory.Element;
using TMPro;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class ItemRecipeView : MonoBehaviour
    {
        private ItemImages _itemImages;
        private BlockGameObjectFactory _blockGameObjectFactory;
        
        [SerializeField] private GameObject craftingRecipeView;
        [SerializeField] private GameObject machineCraftingRecipeView;
        
        [SerializeField] private List<UIBuilderItemSlotObject> craftingRecipeSlots;
        [SerializeField] private UIBuilderItemSlotObject CraftingResultSlotObject;
        
        [SerializeField] private List<UIBuilderItemSlotObject> machineCraftingRecipeSlots;
        [SerializeField] private UIBuilderItemSlotObject MachineCraftingResultSlotObject;
        [SerializeField] private TMP_Text machineNameText;
        
        public event CraftRecipeItemListViewer.ItemSlotClick OnCraftSlotClick;

        [Inject]
        public void Construct(ItemImages itemImages,BlockGameObjectFactory blockGameObjectFactory)
        {
            _blockGameObjectFactory = blockGameObjectFactory;
            _itemImages = itemImages;
            foreach (var slot in craftingRecipeSlots)
            {
                slot.OnLeftClickDown += OnClick;
            }
            foreach (var slot in machineCraftingRecipeSlots)
            {
                slot.OnLeftClickDown += OnClick;
            }
        }
        
        
        private List<ItemStack> _craftItemStacks = new();

        public void SetCraftRecipe(List<ItemStack> itemStacks,ItemStack result)
        {
            craftingRecipeView.SetActive(true);
            machineCraftingRecipeView.SetActive(false);

            for (int i = 0; i < craftingRecipeSlots.Count; i++)
            {
                var item = itemStacks[i];
                craftingRecipeSlots[i].SetItem(_itemImages.GetItemView(item.ID),item.Count);
            }
            CraftingResultSlotObject.SetItem(_itemImages.GetItemView(result.ID),result.Count);

            _craftItemStacks = itemStacks;
        }
        
        private List<ItemStack> _machineCraftItemStacks = new();

        public void SetMachineCraftRecipe(List<ItemStack> itemStacks,ItemStack result,int blockId)
        {
            craftingRecipeView.SetActive(false);
            machineCraftingRecipeView.SetActive(true);

            machineNameText.text = _blockGameObjectFactory.GetName(blockId);
            
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

            MachineCraftingResultSlotObject.SetItem(_itemImages.GetItemView(result.ID),result.Count);
            _machineCraftItemStacks = itemStacks;
        }

        private void OnClick(UIBuilderItemSlotObject uiBuilderItemSlotObject) { OnCraftSlotClick?.Invoke(GetItemStack(uiBuilderItemSlotObject).ID); }


        private ItemStack GetItemStack(UIBuilderItemSlotObject uiBuilderItemSlotObject)
        {
            var craftIndex = craftingRecipeSlots.IndexOf(uiBuilderItemSlotObject);
            var machineCraftIndex = machineCraftingRecipeSlots.IndexOf(uiBuilderItemSlotObject);
            
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