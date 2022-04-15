using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory
{
    public class SubInventorySlotCreator : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;
        [SerializeField] private InventoryArraySlot inventoryArraySlotPrefab;
        public List<InventoryItemSlot> CreateSlots(SubInventoryViewData subInventoryViewData,Transform parent)
        {
            var slots = new List<InventoryItemSlot>();


            foreach (var element in subInventoryViewData.Elements)
            {
                if (element.ElementType == SubInventoryElementType.OneSlot)
                {
                    var slot = Instantiate(inventoryItemSlotPrefab, parent);
                    var rect = slot.GetComponent<RectTransform>();
                    rect.SetAnchor(AnchorPresets.MiddleCenter);
                    
                    var oneSlot = element as OneSlot;
                    rect.anchoredPosition = new Vector2(oneSlot.X, oneSlot.Y);
                    slots.Add(slot);
                    
                }
                else if (element.ElementType == SubInventoryElementType.ArraySlot)
                {
                    var slot = Instantiate(inventoryArraySlotPrefab, parent);
                    var slotSize = inventoryItemSlotPrefab.GetComponent<RectTransform>().sizeDelta;
                    slot.GetComponent<GridLayoutGroup>().cellSize = slotSize;
                    
                    var arraySlot = element as ArraySlot;
                
                    var rect = slot.GetComponent<RectTransform>();
                    rect.SetAnchor(AnchorPresets.MiddleCenter);
                    rect.anchoredPosition = new Vector2(arraySlot.X, arraySlot.Y);
                    rect.sizeDelta = new Vector2(arraySlot.Width * slotSize.x, arraySlot.Height * slotSize.y);
                
                
                    slots.AddRange(slot.SetArraySlot(arraySlot.Height,arraySlot.Width,inventoryItemSlotPrefab));
                    
                }
            }
            return slots;
        }
    }
}