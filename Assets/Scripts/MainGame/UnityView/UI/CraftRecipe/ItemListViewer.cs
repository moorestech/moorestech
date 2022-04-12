using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.CraftRecipe
{
    public class ItemListViewer : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;

        public delegate void ItemSlotClick(int itemId);
        public event ItemSlotClick OnItemListClick;
        
        
        [Inject]
        public void Construct(ItemImages itemImages)
        {
            for (int i = 0; i < itemImages.GetItemNum(); i++)
            {
                var g = Instantiate(inventoryItemSlotPrefab, transform, true);
                g.Construct(i);
                g.SetItem(itemImages.GetItemView(i),0);
                g.SubscribeOnItemSlotClick(InvokeEvent);

                g.transform.localScale = new Vector3(1,1,1);
            }
        }

        public void InvokeEvent(int itemId)
        {
            OnItemListClick?.Invoke(itemId);
        }
    }
}