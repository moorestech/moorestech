using System;
using System.Runtime.CompilerServices;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// ツールチップの印（宿主が最寄りを引く）
    /// Tooltip mark; the host finds the nearest
    /// </summary>
    public static class HoverTooltipMark
    {
        private const string TargetClassName = "hover-tooltip-target";

        // 要素が消えれば印の文も一緒に回収される
        // A mark's text is collected together with its element
        private static readonly ConditionalWeakTable<VisualElement, UiText> TextByElement = new();

        // 印の付け外しと文の差し替えを宿主へ知らせる（ホバー中の印が変わったら移動を待たずに追うため）
        // Tells the host about mark changes so it follows the hovered mark without waiting for a move
        private static readonly Subject<VisualElement> MarkChanged = new();

        public static IObservable<VisualElement> OnMarkChanged => MarkChanged;

        public static void Mark(VisualElement element, UiText text)
        {
            TextByElement.AddOrUpdate(element, text);
            element.AddToClassList(TargetClassName);
            MarkChanged.OnNext(element);
        }

        public static void Unmark(VisualElement element)
        {
            TextByElement.Remove(element);
            element.RemoveFromClassList(TargetClassName);
            MarkChanged.OnNext(element);
        }

        public static bool TryFindMarked(VisualElement from, out VisualElement marked, out UiText text)
        {
            for (var current = from; current != null; current = current.parent)
            {
                if (!TextByElement.TryGetValue(current, out text)) continue;
                marked = current;
                return true;
            }
            marked = null;
            text = null;
            return false;
        }
    }
}
