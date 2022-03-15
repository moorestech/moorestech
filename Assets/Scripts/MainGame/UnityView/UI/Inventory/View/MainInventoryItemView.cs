using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class MainInventoryItemView : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;
        List<InventoryItemSlot> _slots;
        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(ItemImages itemImages)
        {
            _itemImages = itemImages;
            
        }

        public void OnInventoryUpdate(int slot, int itemId, int count)
        {
            var sprite = _itemImages.GetItemViewData(itemId);
            _slots[slot].SetItem(sprite,count);
        }
        
        public IReadOnlyList<InventoryItemSlot> GetInventoryItemSlots()
        {
            if (_slots != null) return _slots;

            _slots = new List<InventoryItemSlot>();
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                var s = Instantiate(inventoryItemSlotPrefab.gameObject, transform).GetComponent<InventoryItemSlot>();
                s.Construct(i);
                _slots.Add(s);
            }
            return _slots;
        }
    }
}