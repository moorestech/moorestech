using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.UnityView.UI.Builder;
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
        [SerializeField] private InventoryTextElement inventoryTextElementPrefab;
        public (List<InventoryItemSlot>,List<GameObject>) CreateSlots(SubInventoryViewBluePrint subInventoryViewBluePrint,Transform parent)
        {
            var slots = new List<InventoryItemSlot>();
            var gameObjects = new List<GameObject>();

            foreach (var element in subInventoryViewBluePrint.Elements)
            {
                switch (element.ElementType)
                {
                    case SubInventoryElementType.OneSlot:
                        var item = CreateOneSlot(element as OneSlot, parent);
                        slots.Add(item);
                        gameObjects.Add(item.gameObject);
                        break;
                    case SubInventoryElementType.ArraySlot:
                        var array = CreateArraySlot(element as ArraySlot, parent);
                        slots.AddRange(array);
                        gameObjects.AddRange(array.Select(x => x.gameObject));
                        break;
                    case SubInventoryElementType.Text:
                        var text = CreateTextElement(element as TextElement, parent);
                        gameObjects.Add(text.gameObject);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(element.ElementType + " の実装がありません");
                }
            }
            return (slots,gameObjects);
        }

        private InventoryTextElement CreateTextElement(TextElement element, Transform parent)
        {
            var text = Instantiate(inventoryTextElementPrefab.gameObject, parent).GetComponent<InventoryTextElement>();
            var rect = text.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(element.X, element.Y);
            
            text.SetText(element.DefaultText,element.FontSize);

            return text;
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