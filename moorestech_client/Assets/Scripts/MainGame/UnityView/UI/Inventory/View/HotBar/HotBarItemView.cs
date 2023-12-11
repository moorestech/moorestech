using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.UIObjects;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View.HotBar
{
    public class HotBarItemView : MonoBehaviour
    {
        [SerializeField] private List<UIBuilderItemSlotObject> hotBarSlots;

        private ItemImages _itemImages;
        private IInventoryItems _inventoryItems;
        
        public IReadOnlyList<UIBuilderItemSlotObject> Slots => hotBarSlots;
        


        [Inject]
        public void Construct(ItemImages itemImages,IInventoryItems inventoryItems)
        {
            _itemImages = itemImages;
            _inventoryItems = inventoryItems;
        }

        private void Update()
        {
            for (int i = 0; i < _inventoryItems.Count(); i++)
            {
                UpdateHotBar(i, _inventoryItems[i]);
            }
        }

        private void UpdateHotBar(int slot,IItemStack  item)
        {
            //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
            var c = PlayerInventoryConstant.MainInventoryColumns;
            var r = PlayerInventoryConstant.MainInventoryRows;
            var startHotBarSlot = c * (r - 1);

            if (slot < startHotBarSlot || PlayerInventoryConstant.MainInventorySize <= slot) return;

            var sprite = _itemImages.GetItemView(item.Id);
            slot -= startHotBarSlot;
            hotBarSlots[slot].SetItem(sprite, item.Count);
        }
    }
}