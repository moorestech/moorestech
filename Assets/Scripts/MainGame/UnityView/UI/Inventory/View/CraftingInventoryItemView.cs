using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MainGame.UnityView.UI.Inventory.View
{
    public class CraftingInventoryItemView : MonoBehaviour
    {
        private const int ResultItemSlot = PlayerInventoryConstant.CraftingInventorySize - 1;
        
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;
        [SerializeField] private Image canNotCraftImage;
        [SerializeField] private RectTransform craftingResultSlot;
        [SerializeField] private InventoryItemSlot craftResultItemView;
        
        List<InventoryItemSlot> _slots;
        private ItemImages _itemImages;


        [Inject]
        public void Construct(ItemImages itemImages)
        {
            _itemImages = itemImages;
        }

        public void OnInventoryUpdate(int slot, ItemStack item)
        {
            var sprite = _itemImages.GetItemViewData(item.ID);
            _slots[slot].SetItem(sprite,item.Count);
        }

        public void SetResultItem(ItemStack resultItem, bool canCraft)
        {
            //結果のアイテムを設定
            craftResultItemView.SetItem(_itemImages.GetItemViewData(resultItem.ID), resultItem.Count);
            //クラフトできない時は矢印にバツを表示する
            canNotCraftImage.gameObject.SetActive(!canCraft);
        }
        
        public IReadOnlyList<InventoryItemSlot> GetInventoryItemSlots()
        {
            if (_slots != null) return _slots;

            _slots = new List<InventoryItemSlot>();
            //クラフトするためのアイテムスロットを作成
            for (int i = 0; i < PlayerInventoryConstant.CraftingSlotSize; i++)
            {
                var s = Instantiate(inventoryItemSlotPrefab.gameObject, transform).GetComponent<InventoryItemSlot>();
                s.Construct(i);
                _slots.Add(s);
            }
            
            //クラフト結果のアイテムスロットを作成
            var result = Instantiate(inventoryItemSlotPrefab.gameObject, craftingResultSlot).GetComponent<InventoryItemSlot>();
            result.Construct(ResultItemSlot);
            _slots.Add(result);
            
            return _slots;
        }
    }
}