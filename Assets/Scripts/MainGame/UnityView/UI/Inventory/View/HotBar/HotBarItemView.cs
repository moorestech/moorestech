using System.Collections.Generic;
using System.Collections.ObjectModel;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View.HotBar
{
    public class HotBarItemView : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;
        List<InventoryItemSlot> _slots;
        public ReadOnlyCollection<InventoryItemSlot> Slots => _slots.AsReadOnly();
        
        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(ItemImages itemImages,PlayerInventoryViewModelController playerInventoryViewModelController)
        {
            _itemImages = itemImages;
            _slots = new List<InventoryItemSlot>();
            for (int i = 0; i < PlayerInventoryConstant.MainInventoryColumns; i++)
            {
                var slot = Instantiate(inventoryItemSlotPrefab.gameObject, transform).GetComponent<InventoryItemSlot>();
                _slots.Add(slot);
            }

            playerInventoryViewModelController.OnSlotUpdate += OnInventoryUpdate;
        }

        public void OnInventoryUpdate(int slot ,ItemStack item)
        {
            //スロットが一番下の段でなければスルー
            var c = PlayerInventoryConstant.MainInventoryColumns;
            var r = PlayerInventoryConstant.MainInventoryRows;
            var startHotBarSlot = c * (r - 1);
            
            if (slot < startHotBarSlot) return;
            
            var sprite = _itemImages.GetItemView(item.ID);
            slot -= startHotBarSlot;
            _slots[slot].SetItem(sprite,item.Count);

        }
    }
}