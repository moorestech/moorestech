using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.Block;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using TMPro;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class CraftingView : MonoBehaviour
    {
        private ItemImages _itemImages;
        private BlockObjects _blockObjects;
        
        [SerializeField] private GameObject craftingRecipeView;
        [SerializeField] private GameObject machineCraftingRecipeView;
        
        [SerializeField] private List<InventoryItemSlot> craftingRecipeSlots;
        [SerializeField] private InventoryItemSlot craftingResultSlot;
        
        [SerializeField] private List<InventoryItemSlot> machineCraftingRecipeSlots;
        [SerializeField] private InventoryItemSlot machineCraftingResultSlot;
        [SerializeField] private TMP_Text MachineNameText;

        
        [Inject]
        public void Construct(ItemImages itemImages,BlockObjects blockObjects)
        {
            _blockObjects = blockObjects;
            _itemImages = itemImages;
        }
        
        public void SetCraftRecipe(List<ItemStack> itemStacks,ItemStack result)
        {
            craftingRecipeView.SetActive(true);
            machineCraftingRecipeView.SetActive(false);

            for (int i = 0; i < craftingRecipeSlots.Count; i++)
            {
                var item = itemStacks[i];
                craftingRecipeSlots[i].SetItem(_itemImages.GetItemViewData(item.ID),item.Count);
            }
            craftingResultSlot.SetItem(_itemImages.GetItemViewData(result.ID),result.Count);
        }
        
        public void SetMachineCraftRecipe(List<ItemStack> itemStacks,ItemStack result,int blockId)
        {
            craftingRecipeView.SetActive(false);
            machineCraftingRecipeView.SetActive(true);

            MachineNameText.text = _blockObjects.GetName(blockId);
            
            for (int i = 0; i < machineCraftingRecipeSlots.Count; i++)
            {
                if (itemStacks.Count <= i)
                {
                    machineCraftingRecipeSlots[i].gameObject.SetActive(false);
                    continue;
                }
                
                machineCraftingRecipeSlots[i].gameObject.SetActive(true);
                var item = itemStacks[i];
                machineCraftingRecipeSlots[i].SetItem(_itemImages.GetItemViewData(item.ID),item.Count);
            }
            machineCraftingResultSlot.SetItem(_itemImages.GetItemViewData(result.ID),result.Count);
        }
    }
}