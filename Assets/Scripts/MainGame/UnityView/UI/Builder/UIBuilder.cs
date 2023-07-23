using System;
using System.Collections.Generic;
using System.Linq;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Element;
using MainGame.UnityView.UI.Builder.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Builder
{
    /// <summary>
    /// サブインベントリを構築するブループリントを受け取り、実際のGameObjectのUIを構築する
    /// </summary>
    public class UIBuilder : MonoBehaviour
    {
        [SerializeField] private UIBuilderItemSlotObject UIBuilderItemSlotObjectPrefab;
        [SerializeField] private UIBuilderItemSlotArrayObject UIBuilderItemSlotArrayObjectPrefab;
        [SerializeField] private UIBuilderTextObject UIBuilderTextObjectPrefab;
        [SerializeField] private UIBuilderProgressArrowObject UIBuilderProgressArrowObjectPrefab;
        
        /// <summary>
        /// ブループリントからそのオブジェクトを生成する
        /// </summary>
        /// <param name="inventoryViewBluePrint">構築するUIのブループリント</param>
        /// <param name="parent">作成するオブジェクトの親</param>
        /// <returns>インベントリのスロットのオブジェクト,　スロット以外のオブジェクト（テキストなど）</returns>
        public List<IUIBuilderObject> CreateSlots(IInventoryViewBluePrint inventoryViewBluePrint,Transform parent)
        {
            var uiObjects = new List<IUIBuilderObject>();

            foreach (var element in inventoryViewBluePrint.Elements)
            {
                switch (element.ElementElementType)
                {
                    case UIBluePrintElementType.OneSlot:
                        var item = CreateOneSlot(element as UIBluePrintItemSlot, parent);
                        uiObjects.Add(item);
                        break;
                    case UIBluePrintElementType.ArraySlot:
                        var array = CreateArraySlot(element as UIBluePrintItemSlotArray, parent);
                        uiObjects.AddRange(array);
                        break;
                    case UIBluePrintElementType.Text:
                        var text = CreateTextElement(element as UIBluePrintText, parent);
                        uiObjects.Add(text);
                        break;
                    case UIBluePrintElementType.ProgressArrow:
                        var arrow = CreateProgressArrow(element as UIBluePrintProgressArrow, parent);
                        uiObjects.Add(arrow);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(element.ElementElementType + " の実装がありません");
                }
            }
            return uiObjects;
        }

        private UIBuilderTextObject CreateTextElement(UIBluePrintText element, Transform parent)
        {
            var text = Instantiate(UIBuilderTextObjectPrefab.gameObject, parent).GetComponent<UIBuilderTextObject>();
            text.Initialize(element);
            
            var rect = text.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(element.X, element.Y);
            
            text.SetText(element.DefaultText,element.FontSize);

            return text;
        }


        private UIBuilderItemSlotObject CreateOneSlot(UIBluePrintItemSlot uiBluePrintItemSlot,Transform parent)
        {
            var slot = Instantiate(UIBuilderItemSlotObjectPrefab, parent);
            slot.Initialize(uiBluePrintItemSlot);
            
            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(uiBluePrintItemSlot.X, uiBluePrintItemSlot.Y);
                    
            slot.SetSlotOptions(uiBluePrintItemSlot.Options);

            return slot;
        }


        private List<UIBuilderItemSlotObject> CreateArraySlot(UIBluePrintItemSlotArray uiBluePrintItemSlotArray, Transform parent)
        {
            var slot = Instantiate(UIBuilderItemSlotArrayObjectPrefab, parent);
            slot.Initialize(uiBluePrintItemSlotArray);
            
            var slotSize = UIBuilderItemSlotObjectPrefab.GetComponent<RectTransform>().sizeDelta;
            slot.GetComponent<GridLayoutGroup>().cellSize = slotSize;
                
            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(uiBluePrintItemSlotArray.X, uiBluePrintItemSlotArray.Y);
            rect.sizeDelta = new Vector2(uiBluePrintItemSlotArray.Width * slotSize.x, uiBluePrintItemSlotArray.Height * slotSize.y);

            return slot.SetArraySlot(uiBluePrintItemSlotArray.Height, uiBluePrintItemSlotArray.Width, uiBluePrintItemSlotArray.BottomBlank);
        }


        private UIBuilderProgressArrowObject CreateProgressArrow(UIBluePrintProgressArrow uiBluePrintProgressArrow, Transform parent)
        {
            var arrow = Instantiate(UIBuilderProgressArrowObjectPrefab, parent);
            arrow.Initialize(uiBluePrintProgressArrow);
            
            var rect = arrow.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = new Vector2(uiBluePrintProgressArrow.X, uiBluePrintProgressArrow.Y);

            return arrow;
        }

    }
}