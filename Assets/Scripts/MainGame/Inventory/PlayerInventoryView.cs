using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.Inventory
{
    public class PlayerInventoryView : MonoBehaviour
    {
        [SerializeField] private List<InventoryItemSlot> mainInventorySlots;
        [SerializeField] private InventoryItemSlot equippedItem;

        private ItemImages _itemImages;

        [Inject]
        public void Construct(PlayerInventoryModel playerInventoryModel,ItemImages itemImages)
        {
            _itemImages = itemImages;
            for (var i = 0; i < playerInventoryModel.MainInventory.Count; i++)
            {
                var item = playerInventoryModel.MainInventory[i];
                mainInventorySlots[i].SetItem(_itemImages.GetItemView(item.ID),item.Count);
            }

            playerInventoryModel.OnItemEquipped += () => equippedItem.gameObject.SetActive(true);
            playerInventoryModel.OnItemUnequipped += () => equippedItem.gameObject.SetActive(false);
            
            
            playerInventoryModel.OnSlotUpdate += (slot, item) => mainInventorySlots[slot].SetItem(_itemImages.GetItemView(item.ID),item.Count);
            playerInventoryModel.OnEquippedItemUpdate += item => equippedItem.SetItem(_itemImages.GetItemView(item.ID),item.Count);
        }
    }
}