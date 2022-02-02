using System.Collections.Generic;
using MainGame.Constant;
using MainGame.UnityView.Interface;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory
{
    public class MainInventoryView : MonoBehaviour
    {
        [SerializeField] private InventorySlot inventorySlot;
        List<InventorySlot> _slots;
        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(IInventoryUpdateEvent inventoryUpdateEvent,ItemImages itemImages)
        {
            inventoryUpdateEvent.Subscribe(OnInventoryUpdate);
            _itemImages = itemImages;
            
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                _slots.Add(Instantiate(inventorySlot.gameObject,transform).GetComponent<InventorySlot>());
            }
        }

        private void OnInventoryUpdate(int slot, int itemId, int count)
        {
            var sprite = _itemImages.GetItemImage(itemId);
            _slots[slot].SetItem(sprite,count);
        }
    }
}