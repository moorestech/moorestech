using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class CraftingInventoryItemView : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlot;
        [SerializeField] private RectTransform craftingResultSlot;
        
        List<InventoryItemSlot> _slots;
        private ItemImages _itemImages;
        
        
        [Inject]
        public void Construct(ItemImages itemImages)
        {
            _itemImages = itemImages;
        }

        public void OnInventoryUpdate(int slot, int itemId, int count)
        {
            var sprite = _itemImages.GetItemImage(itemId);
            _slots[slot].SetItem(sprite,count);
        }
        
        public IReadOnlyList<InventoryItemSlot> GetInventoryItemSlots()
        {
            if (_slots != null) return _slots;

            _slots = new List<InventoryItemSlot>();
            //クラフトするためのアイテムスロットを作成
            for (int i = 0; i < PlayerInventoryConstant.CraftingSlotSize; i++)
            {
                var s = Instantiate(inventoryItemSlot.gameObject, transform).GetComponent<InventoryItemSlot>();
                s.Construct(i);
                _slots.Add(s);
            }
            
            //クラフト結果のアイテムスロットを作成
            var result = Instantiate(inventoryItemSlot.gameObject, craftingResultSlot).GetComponent<InventoryItemSlot>();
            result.Construct(PlayerInventoryConstant.CraftingSlotSize - 1);
            _slots.Add(result);
            
            return _slots;
        }
    }
}