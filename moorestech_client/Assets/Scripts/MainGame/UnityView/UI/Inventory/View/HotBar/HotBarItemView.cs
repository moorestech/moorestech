using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MainGame.Basic;
using MainGame.UnityView.UI.Builder;
using MainGame.UnityView.UI.Builder.Unity;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View.HotBar
{
    public class HotBarItemView : MonoBehaviour
    {
        [SerializeField] List<UIBuilderItemSlotObject> hotBarSlots;
        public IReadOnlyList<UIBuilderItemSlotObject> Slots => hotBarSlots;

        private ItemImages _itemImages;


        [Inject]
        public void Construct(ItemImages itemImages,PlayerInventoryViewModelController playerInventoryViewModelController)
        {
            _itemImages = itemImages;
            playerInventoryViewModelController.OnSlotUpdate += OnInventoryUpdate;
        }

        private void OnInventoryUpdate(int slot ,ItemStack item)
        {
            //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
            var c = PlayerInventoryConstant.MainInventoryColumns;
            var r = PlayerInventoryConstant.MainInventoryRows;
            var startHotBarSlot = c * (r - 1);
            
            if (slot < startHotBarSlot || PlayerInventoryConstant.MainInventorySize <= slot) return;
            
            var sprite = _itemImages.GetItemView(item.ID);
            slot -= startHotBarSlot;
            hotBarSlots[slot].SetItem(sprite,item.Count);
        }
    }
}