using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class PlayerInventoryPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        [SerializeField] private InventoryItemSlot equippedItem;

        [Inject]
        public void Construct(PlayerInventoryModelController playerInventoryModelController,ItemImages itemImages,PlayerInventoryModel playerInventoryModel)
        {

            playerInventoryModelController.OnItemEquipped += () => equippedItem.gameObject.SetActive(true);
            playerInventoryModelController.OnItemUnequipped += () => equippedItem.gameObject.SetActive(false);
            
            
            playerInventoryModelController.OnSlotUpdate += (slot, item) => playerInventorySlots.SetImage(slot,itemImages.GetItemView(item.ID),item.Count);
            playerInventoryModelController.OnEquippedItemUpdate += item => equippedItem.SetItem(itemImages.GetItemView(item.ID),item.Count);

            playerInventoryModel.OnInventoryUpdate += () =>
            {
                for (var i = 0; i < playerInventoryModel.Count; i++)
                {
                    var item = playerInventoryModel[i];
                    playerInventorySlots.SetImage(i,itemImages.GetItemView(item.Id),item.Count);
                }
            };
        }
    }
}