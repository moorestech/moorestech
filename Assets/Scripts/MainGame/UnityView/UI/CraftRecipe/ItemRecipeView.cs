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
        
        [SerializeField] private GameObject craftingRecipeView;
        [SerializeField] private GameObject machineCraftingRecipeView;
        
        [SerializeField] private List<UIBuilderItemSlotObject> craftingRecipeSlots;
        [SerializeField] private UIBuilderItemSlotObject CraftingResultSlotObject;
        
        [SerializeField] private List<UIBuilderItemSlotObject> machineCraftingRecipeSlots;
        [SerializeField] private UIBuilderItemSlotObject MachineCraftingResultSlotObject;
        [SerializeField] private UIBuilderItemSlotObject MachineSlotObject;
        [SerializeField] private TMP_Text machineNameText;
        
        public event CraftRecipeItemListViewer.ItemSlotClick OnCraftSlotClick;

        [Inject]
        public void Construct(ItemImages itemImages,BlockGameObjectFactory blockGameObjectFactory)
        {
            _itemImages = itemImages;
            foreach (var slot in craftingRecipeSlots)
            {
                slot.OnLeftClickDown += OnClick;
            }
            foreach (var slot in machineCraftingRecipeSlots)
            {
                slot.OnLeftClickDown += OnClick;
            }
            MachineSlotObject.OnLeftClickDown += OnClick;
        }
        private void OnClick(UIBuilderItemSlotObject uiBuilderItemSlotObject) { OnCraftSlotClick?.Invoke(uiBuilderItemSlotObject.ItemViewData.ItemId); }


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
        }
        
        public void SetMachineCraftRecipe(List<ItemStack> itemStacks,ItemStack result,int machineItemId)
        {
            craftingRecipeView.SetActive(false);
            machineCraftingRecipeView.SetActive(true);
            
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

            var machineItem = _itemImages.GetItemView(machineItemId);
            machineNameText.text = machineItem.ItemName;
            MachineSlotObject.SetItem(machineItem,0);
            MachineCraftingResultSlotObject.SetItem(_itemImages.GetItemView(result.ID),result.Count);
        }
    }
}