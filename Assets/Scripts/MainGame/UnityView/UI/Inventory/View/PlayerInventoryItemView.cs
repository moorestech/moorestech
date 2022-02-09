using System.Collections.Generic;
using MainGame.Constant;
using MainGame.UnityView.Interface;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class PlayerInventoryItemView : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlot;
        List<InventoryItemSlot> _slots = new();
        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(IPlayerInventoryViewUpdateEvent playerInventoryViewUpdateEvent,ItemImages itemImages)
        {
            playerInventoryViewUpdateEvent.Subscribe(OnInventoryUpdate);
            _itemImages = itemImages;
            
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                var s = Instantiate(inventoryItemSlot.gameObject, transform).GetComponent<InventoryItemSlot>();
                s.Construct(i);
                _slots.Add(s);
            }
        }

        private void OnInventoryUpdate(int slot, int itemId, int count)
        {
            var sprite = _itemImages.GetItemImage(itemId);
            _slots[slot].SetItem(sprite,count);
        }
        
        public IReadOnlyList<InventoryItemSlot> GetInventoryItemSlots()
        {
            return _slots;
        }
    }
}