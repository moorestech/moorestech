using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Constant;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.Main;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.HotBar
{
    public class HotBarItemView : MonoBehaviour
    {
        [SerializeField] private List<ItemSlotObject> hotBarSlots;

        private ItemImageContainer _itemImageContainer;
        private IInventoryItems _inventoryItems;
        
        public IReadOnlyList<ItemSlotObject> Slots => hotBarSlots;
        


        [Inject]
        public void Construct(ItemImageContainer itemImageContainer,IInventoryItems inventoryItems)
        {
            _itemImageContainer = itemImageContainer;
            _inventoryItems = inventoryItems;
        }

        private void Update()
        {
            for (int i = 0; i < _inventoryItems.Count(); i++)
            {
                UpdateHotBar(i, _inventoryItems[i]);
            }
        }

        private void UpdateHotBar(int slot,IItemStack item)
        {
            //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
            var c = PlayerInventoryConstant.MainInventoryColumns;
            var r = PlayerInventoryConstant.MainInventoryRows;
            var startHotBarSlot = c * (r - 1);

            if (slot < startHotBarSlot || PlayerInventoryConstant.MainInventorySize <= slot) return;

            var viewData = _itemImageContainer.GetItemView(item.Id);
            slot -= startHotBarSlot;
            hotBarSlots[slot].SetItem(viewData, item.Count);
        }
    }
}