using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Builder
{
    public class UIBuilder : MonoBehaviour
    {
        [SerializeField] private UIBuilderItemSlotObject UIBuilderItemSlotObjectPrefab;
        [SerializeField] private UIBuilderItemSlotArrayObject UIBuilderItemSlotArrayObjectPrefab;
        [SerializeField] private UIBuilderTextObject UIBuilderTextObjectPrefab;
        public (List<UIBuilderItemSlotObject>,List<GameObject>) CreateSlots(SubInventoryViewBluePrint subInventoryViewBluePrint,Transform parent)
        {
            var slots = new List<UIBuilderItemSlotObject>();
            var gameObjects = new List<GameObject>();

            foreach (var element in subInventoryViewBluePrint.Elements)
            {
                switch (element.ElementType)
                {
                    case UIBluePrintType.OneSlot:
                        var item = CreateOneSlot(element as UIBluePrintItemSlot, parent);
                        slots.Add(item);
                        gameObjects.Add(item.gameObject);
                        break;
                    case UIBluePrintType.ArraySlot:
                        var array = CreateArraySlot(element as UIBluePrintItemSlotArray, parent);
                        slots.AddRange(array);
                        gameObjects.AddRange(array.Select(x => x.gameObject));
                        break;
                    case UIBluePrintType.Text:
                        var text = CreateTextElement(element as TextElement, parent);
                        gameObjects.Add(text.gameObject);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(element.ElementType + " の実装がありません");
                }
            }
            return (slots,gameObjects);
        }

        private UIBuilderTextObject CreateTextElement(TextElement element, Transform parent)
        {
            var text = Instantiate(UIBuilderTextObjectPrefab.gameObject, parent).GetComponent<UIBuilderTextObject>();
            var rect = text.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(element.X, element.Y);
            
            text.SetText(element.DefaultText,element.FontSize);

            return text;
        }


        private UIBuilderItemSlotObject CreateOneSlot(UIBluePrintItemSlot uiBluePrintItemSlot,Transform parent)
        {
            var slot = Instantiate(UIBuilderItemSlotObjectPrefab, parent);
            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(uiBluePrintItemSlot.X, uiBluePrintItemSlot.Y);
                    
            slot.SetSlotOptions(uiBluePrintItemSlot.Options);

            return slot;
        }


        private List<UIBuilderItemSlotObject> CreateArraySlot(UIBluePrintItemSlotArray uiBluePrintItemSlotArray, Transform parent)
        {
            var slot = Instantiate(UIBuilderItemSlotArrayObjectPrefab, parent);
            var slotSize = UIBuilderItemSlotObjectPrefab.GetComponent<RectTransform>().sizeDelta;
            slot.GetComponent<GridLayoutGroup>().cellSize = slotSize;
                
            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(uiBluePrintItemSlotArray.X, uiBluePrintItemSlotArray.Y);
            rect.sizeDelta = new Vector2(uiBluePrintItemSlotArray.Width * slotSize.x, uiBluePrintItemSlotArray.Height * slotSize.y);

            return slot.SetArraySlot(uiBluePrintItemSlotArray.Height, uiBluePrintItemSlotArray.Width, uiBluePrintItemSlotArray.BottomBlank);
        }
        

    }
}