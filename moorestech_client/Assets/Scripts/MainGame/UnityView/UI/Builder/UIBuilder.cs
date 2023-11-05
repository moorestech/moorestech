using System;
using System.Collections.Generic;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Builder.Element;
using MainGame.UnityView.UI.Builder.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace MainGame.UnityView.UI.Builder
{
    /// <summary>
    ///     サブインベントリを構築するブループリントを受け取り、実際のGameObjectのUIを構築する
    /// </summary>
    public class UIBuilder : MonoBehaviour
    {
        [SerializeField] private UIBuilderItemSlotObject UIBuilderItemSlotObjectPrefab;
        [SerializeField] private UIBuilderItemSlotArrayObject UIBuilderItemSlotArrayObjectPrefab;
        [SerializeField] private UIBuilderTextObject UIBuilderTextObjectPrefab;
        [SerializeField] private UIBuilderProgressArrowObject UIBuilderProgressArrowObjectPrefab;

        /// <summary>
        ///     ブループリントからそのオブジェクトを生成する
        /// </summary>
        /// <param name="inventoryViewBluePrint">構築するUIのブループリント</param>
        /// <param name="parent">作成するオブジェクトの親</param>
        /// <returns>インベントリのスロットのオブジェクト,　スロット以外のオブジェクト（テキストなど）</returns>
        public List<IUIBuilderObject> CreateSlots(IInventoryViewBluePrint inventoryViewBluePrint, Transform parent)
        {
            var uiObjects = new List<IUIBuilderObject>();

            foreach (var element in inventoryViewBluePrint.Elements)
                switch (element.ElementElementType)
                {
                    //TODO サイズの設定方法とArrayの設定について考える必要がある
                    case UIBluePrintElementType.OneSlot:
                        uiObjects.Add(CreateOneSlot(element as UIBluePrintItemSlot, parent));
                        break;
                    case UIBluePrintElementType.ArraySlot:
                        uiObjects.AddRange(CreateArraySlot(element as UIBluePrintItemSlotArray, parent));
                        break;
                    case UIBluePrintElementType.Text:
                        uiObjects.Add(CreateTextElement(element as UIBluePrintText, parent));
                        break;
                    case UIBluePrintElementType.ProgressArrow:
                        uiObjects.Add(CreateProgressArrow(element as UIBluePrintProgressArrow, parent));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(element.ElementElementType + " の実装がありません");
                }

            return uiObjects;
        }

        private UIBuilderTextObject CreateTextElement(UIBluePrintText element, Transform parent)
        {
            var text = Instantiate(UIBuilderTextObjectPrefab.gameObject, parent).GetComponent<UIBuilderTextObject>();
            text.Initialize(element);

            var rect = text.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = element.RectPosition;
            rect.rotation = Quaternion.Euler(element.Rotation);
            rect.sizeDelta = element.RectSize;

            text.SetText(element.DefaultText, element.FontSize);

            return text;
        }


        private UIBuilderItemSlotObject CreateOneSlot(UIBluePrintItemSlot uiBluePrintItemSlot, Transform parent)
        {
            var slot = Instantiate(UIBuilderItemSlotObjectPrefab, parent);
            //intefaceの定義の関係でこうしている
            slot.Initialize(uiBluePrintItemSlot);
            slot.SetSlotOptions(uiBluePrintItemSlot.Options);

            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = uiBluePrintItemSlot.RectPosition;
            rect.rotation = Quaternion.Euler(uiBluePrintItemSlot.Rotation);
            rect.sizeDelta = uiBluePrintItemSlot.RectSize;


            return slot;
        }


        private List<UIBuilderItemSlotObject> CreateArraySlot(UIBluePrintItemSlotArray uiBluePrintItemSlotArray, Transform parent)
        {
            var slot = Instantiate(UIBuilderItemSlotArrayObjectPrefab, parent);
            slot.Initialize(uiBluePrintItemSlotArray);

            var slotSize = UIBuilderItemSlotObjectPrefab.GetComponent<RectTransform>().sizeDelta;
            slot.GetComponent<GridLayoutGroup>().cellSize = slotSize;

            //TODO ArrayだけRectTransformの設定が特殊だからこうしている　設計を考える必要あり
            //TODO Arrayっていう概念をなくして拡張メソッド的なので定義できるようにするのもあり

            var rect = slot.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = uiBluePrintItemSlotArray.RectPosition;
            rect.rotation = Quaternion.Euler(uiBluePrintItemSlotArray.Rotation);
            rect.sizeDelta = uiBluePrintItemSlotArray.RectSize;

            return slot.SetArraySlot(uiBluePrintItemSlotArray.ArrayRow, uiBluePrintItemSlotArray.ArrayColumn, uiBluePrintItemSlotArray.BottomBlank);
        }


        private UIBuilderProgressArrowObject CreateProgressArrow(UIBluePrintProgressArrow uiBluePrintProgressArrow, Transform parent)
        {
            var arrow = Instantiate(UIBuilderProgressArrowObjectPrefab, parent);
            arrow.Initialize(uiBluePrintProgressArrow);

            var rect = arrow.GetComponent<RectTransform>();
            rect.SetAnchor(AnchorPresets.MiddleCenter);
            rect.anchoredPosition = uiBluePrintProgressArrow.RectPosition;
            rect.rotation = Quaternion.Euler(uiBluePrintProgressArrow.Rotation);
            rect.sizeDelta = uiBluePrintProgressArrow.RectSize;

            return arrow;
        }
    }
}