using System.Collections.Generic;
using MainGame.Constant;
using MainGame.UnityView.Interface;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory
{
    public class HotBarItemView : MonoBehaviour
    {
        
        [SerializeField] private InventoryItemSlot inventoryItemSlot;
        List<InventoryItemSlot> _slots;
        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(IInventoryUpdateEvent inventoryUpdateEvent,ItemImages itemImages)
        {
            inventoryUpdateEvent.Subscribe(OnInventoryUpdate);
            _itemImages = itemImages;
            
            for (int i = 0; i < PlayerInventoryConstant.MainInventoryColumns; i++)
            {
                _slots.Add(Instantiate(inventoryItemSlot.gameObject,transform).GetComponent<InventoryItemSlot>());
            }
        }

        private void OnInventoryUpdate(int slot, int itemId, int count)
        {
            //スロットが一番下の段でなければスルー
            var c = PlayerInventoryConstant.MainInventoryColumns;
            var r = PlayerInventoryConstant.MainInventoryRows;
            var startHotBarSlot = c * (r - 1);
            if (startHotBarSlot <= slot) return;
            
            var sprite = _itemImages.GetItemImage(itemId);
            _slots[slot].SetItem(sprite,count);
        }
    }
}