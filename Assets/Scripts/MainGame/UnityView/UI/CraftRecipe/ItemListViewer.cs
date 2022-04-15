using System.Collections.Generic;
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
        private readonly Dictionary<InventoryItemSlot, int> _itemIdTable = new();


        [Inject]
        public void Construct(ItemImages itemImages)
        {
            for (int i = 0; i < itemImages.GetItemNum(); i++)
            {
                var g = Instantiate(inventoryItemSlotPrefab, transform, true);
                g.SetItem(itemImages.GetItemView(i),0);
                g.OnLeftClickDown += InvokeEvent;
                _itemIdTable.Add(g,i);

                g.transform.localScale = new Vector3(1,1,1);
            }
        }

        public void InvokeEvent(InventoryItemSlot inventoryItem)
        {
            OnItemListClick?.Invoke(_itemIdTable[inventoryItem]);
        }
    }
}