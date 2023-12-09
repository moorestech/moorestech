using System.Collections.Generic;
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
        public IReadOnlyList<UIBuilderItemSlotObject> Slots => hotBarSlots;


        [Inject]
        public void Construct(ItemImages itemImages)
        {
            _itemImages = itemImages;
            OnInventoryUpdate(0, new ItemStack(0, 1));
        }

        public void OnInventoryUpdate(int slot, ItemStack item)
        {
            throw new System.NotImplementedException();
            //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
            var c = PlayerInventoryConstant.MainInventoryColumns;
            var r = PlayerInventoryConstant.MainInventoryRows;
            var startHotBarSlot = c * (r - 1);

            if (slot < startHotBarSlot || PlayerInventoryConstant.MainInventorySize <= slot) return;

            var sprite = _itemImages.GetItemView(item.ID);
            slot -= startHotBarSlot;
            hotBarSlots[slot].SetItem(sprite, item.Count);
        }
    }
}