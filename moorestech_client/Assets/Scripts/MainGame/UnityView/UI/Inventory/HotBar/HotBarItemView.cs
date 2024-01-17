using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface;
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
        private ILocalPlayerInventory _localPlayerInventory;
        
        public IReadOnlyList<ItemSlotObject> Slots => hotBarSlots;
        


        [Inject]
        public void Construct(ItemImageContainer itemImageContainer,ILocalPlayerInventory localPlayerInventory)
        {
            _itemImageContainer = itemImageContainer;
            _localPlayerInventory = localPlayerInventory;
        }

        private void Update()
        {
            for (int i = 0; i < _localPlayerInventory.Count(); i++)
            {
                UpdateHotBar(i, _localPlayerInventory[i]);
            }
        }

        private void UpdateHotBar(int slot,IItemStack item)
        {
            //スロットが一番下の段もしくはメインインベントリの範囲外の時はスルー
            var c = PlayerInventoryConst.MainInventoryColumns;
            var r = PlayerInventoryConst.MainInventoryRows;
            var startHotBarSlot = c * (r - 1);

            if (slot < startHotBarSlot || PlayerInventoryConst.MainInventorySize <= slot) return;

            var viewData = _itemImageContainer.GetItemView(item.Id);
            slot -= startHotBarSlot;
            hotBarSlots[slot].SetItem(viewData, item.Count);
        }
    }
}