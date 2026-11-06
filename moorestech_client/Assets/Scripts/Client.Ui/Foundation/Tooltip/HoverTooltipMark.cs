using System.Runtime.CompilerServices;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// ホバーのツールチップを出す要素の印。部品が文を付け、宿主（C3-b の HoverTooltipHost）が指した要素から最も近い印を引く（Web の shared/ui/HoverTooltip・ADR 0019）
    /// Mark for elements that show a hover tooltip: components attach the text, and the host (HoverTooltipHost in C3-b) finds the nearest mark from the hovered element (Web shared/ui/HoverTooltip, ADR 0019)
    /// </summary>
    public static class HoverTooltipMark
    {
        public const string TargetClassName = "hover-tooltip-target";

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
            // 指した要素から親へ辿り、最初に見つけた印を返す
            // Walk from the hovered element up through its parents and return the first mark found
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
