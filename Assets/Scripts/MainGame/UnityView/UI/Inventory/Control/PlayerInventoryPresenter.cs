using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MainGame.UnityView.UI.Inventory.Control
{
    public class PlayerInventoryPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        [SerializeField] private InventoryItemSlot grabbedItem;

        [Inject]
        public void Construct(PlayerInventoryViewModelController playerInventoryViewModelController,ItemImages itemImages,PlayerInventoryViewModel playerInventoryViewModel)
        {
            playerInventoryViewModelController.OnItemGrabbed += (_,_) => grabbedItem.gameObject.SetActive(true);
            playerInventoryViewModelController.OnItemUnGrabbed += () => grabbedItem.gameObject.SetActive(false);
            playerInventoryViewModelController.OnGrabbedItemUpdate += item => grabbedItem.SetItem(itemImages.GetItemView(item.ID),item.Count);
            
            playerInventoryViewModelController.OnSlotUpdate += (slot, item) => playerInventorySlots.SetImage(slot,itemImages.GetItemView(item.ID),item.Count);

            playerInventoryViewModel.OnInventoryUpdate += () =>
            {
                for (var i = 0; i < playerInventoryViewModel.Count; i++)
                {
                    var item = playerInventoryViewModel[i];
                    playerInventorySlots.SetImage(i,itemImages.GetItemView(item.Id),item.Count);
                }
            };
        }
    }
}