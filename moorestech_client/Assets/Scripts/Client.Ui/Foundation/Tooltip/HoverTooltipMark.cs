using System.Runtime.CompilerServices;
using Client.Ui.State.Text;
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

        public static void Mark(VisualElement element, UiText text)
        {
            TextByElement.AddOrUpdate(element, text);
            element.AddToClassList(TargetClassName);
        }

        public static void Unmark(VisualElement element)
        {
            TextByElement.Remove(element);
            element.RemoveFromClassList(TargetClassName);
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
