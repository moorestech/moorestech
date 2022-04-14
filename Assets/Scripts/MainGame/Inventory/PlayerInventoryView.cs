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
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        [SerializeField] private InventoryItemSlot equippedItem;

        private ItemImages _itemImages;

        [Inject]
        public void Construct(PlayerInventoryModel playerInventoryModel,ItemImages itemImages)
        {
            _itemImages = itemImages;
            for (var i = 0; i < playerInventoryModel.MainInventory.Count; i++)
            {
                var item = playerInventoryModel.MainInventory[i];
                playerInventorySlots.SetImage(i,_itemImages.GetItemView(item.ID),item.Count);
            }

            playerInventoryModel.OnItemEquipped += () => equippedItem.gameObject.SetActive(true);
            playerInventoryModel.OnItemUnequipped += () => equippedItem.gameObject.SetActive(false);
            
            
            playerInventoryModel.OnSlotUpdate += (slot, item) => playerInventorySlots.SetImage(slot,_itemImages.GetItemView(item.ID),item.Count);
            playerInventoryModel.OnEquippedItemUpdate += item => equippedItem.SetItem(_itemImages.GetItemView(item.ID),item.Count);
        }
    }
}