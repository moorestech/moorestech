using System;
using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View.SubInventory.Element;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Inventory.View.SubInventory
{
    public class SubInventorySlotCreator : MonoBehaviour
    {
        [SerializeField] private InventoryItemSlot inventoryItemSlotPrefab;
        [SerializeField] private InventoryArraySlot inventoryArraySlotPrefab;
        public List<InventoryItemSlot> CreateSlots(SubInventoryViewBluePrint subInventoryViewBluePrint,Transform parent)
        {
            var slots = new List<InventoryItemSlot>();


            foreach (var element in subInventoryViewBluePrint.Elements)
            {
                switch (element.ElementType)
                {
                    case SubInventoryElementType.OneSlot:
                        slots.Add(CreateOneSlot(element as OneSlot, parent));
                        break;
                    case SubInventoryElementType.ArraySlot:
                        slots.AddRange(CreateArraySlot(element as ArraySlot, parent));
                        break;
                    case SubInventoryElementType.Text:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(element.ElementType + " の実装がありません");
                }
            }
            return slots;
        }


        
        
        
        private InventoryItemSlot CreateOneSlot(OneSlot oneSlot,Transform parent)
        {
            var slot = Instantiate(inventoryItemSlotPrefab, parent);
            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            
            rect.anchoredPosition = new Vector2(oneSlot.X, oneSlot.Y);
                    
            slot.SetSlotOptions(oneSlot.Options);

            return slot;
        }


        private List<InventoryItemSlot> CreateArraySlot(ArraySlot arraySlot, Transform parent)
        {
            var slot = Instantiate(inventoryArraySlotPrefab, parent);
            var slotSize = inventoryItemSlotPrefab.GetComponent<RectTransform>().sizeDelta;
            slot.GetComponent<GridLayoutGroup>().cellSize = slotSize;
                
            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(arraySlot.X, arraySlot.Y);
            rect.sizeDelta = new Vector2(arraySlot.Width * slotSize.x, arraySlot.Height * slotSize.y);

            return slot.SetArraySlot(arraySlot.Height, arraySlot.Width, arraySlot.BottomBlank, inventoryItemSlotPrefab);
        }
        

    }
}