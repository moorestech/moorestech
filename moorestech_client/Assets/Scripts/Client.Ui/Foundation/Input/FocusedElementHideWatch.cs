using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input
{
    /// <summary>
    /// フォーカス中の要素が表示されなくなったらフォーカスを外す（Web のフォーカスの直しと同じ。画面は隠すときに Blur しない）
    /// Blurs the focused element once it stops being displayed (the Web focus fixup; screens never blur when hiding)
    /// </summary>
    /// <remarks>
    /// 祖先の display: none は配下の配置を 0 にして GeometryChangedEvent を出すので、フォーカス中の要素の配置の変化だけを購読する。木から外れた分は GameUiInputRules が取り外しの購読で旗を下ろす
    /// An ancestor's display: none zeroes the subtree layout and raises GeometryChangedEvent, so only the focused element's layout changes are watched; removal from the tree is handled by GameUiInputRules through its detach subscription
    /// </remarks>
    internal static class FocusedElementHideWatch
    {
        public static void Watch(VisualElement focused)
        {
            focused.RegisterCallback<GeometryChangedEvent>(OnFocusedGeometryChanged);
        }

        public static void Unwatch(VisualElement element)
        {
            element.UnregisterCallback<GeometryChangedEvent>(OnFocusedGeometryChanged);
        }

        private static void OnFocusedGeometryChanged(GeometryChangedEvent evt)
        {
            // silent-ok: 表示されたままの移動・寸法の変化は外す理由が無い / フォーカスは保たれ入力中の旗も正しいまま
            // silent-ok: moves and resizes while still displayed give no reason to blur / focus stays and the text-input flag stays correct
            var focused = (VisualElement)evt.currentTarget;
            if (IsDisplayed(focused)) return;

            // 外すと根の入力規則へフォーカスの外れが届き、入力中の旗が下りる
            // Blurring sends the focus-out through the root input rules, which lowers the text-input flag
            Unwatch(focused);
            focused.Blur();
        }

        // display は継承されないので祖先まで辿る
        // display does not inherit, so walk up the ancestors
        private static bool IsDisplayed(VisualElement element)
        {
            for (var current = element; current != null; current = current.hierarchy.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }
    }
}
