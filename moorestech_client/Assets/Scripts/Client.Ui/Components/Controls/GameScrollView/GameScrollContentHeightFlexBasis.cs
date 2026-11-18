using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 縦のスクロール領域の flex-basis を中身の高さへ追従させる
    /// Keeps a vertical scroll area's flex-basis tracking its content height
    /// </summary>
    /// <remarks>
    /// Web の ScrollArea の flex: 1（basis 0%）は、高さが内容で決まる親では内容の高さとして解かれ、親が max-height に当たると兄弟と比で縮む。それを写すため basis を中身の高さに追従させる。同じメソッドの登録は UITK が 1 つにまとめるので、2 回呼んでも購読は 1 つ
    /// On the Web, ScrollArea's flex: 1 (basis 0%) resolves to the content height inside a content-sized parent, which then shrinks alongside its siblings at max-height; mirror that by tracking the content height as the basis. UITK keeps one registration per method, so calling twice still subscribes once
    /// </remarks>
    internal static class GameScrollContentHeightFlexBasis
    {
        public static void Follow(ScrollView scrollView)
        {
            scrollView.contentContainer.RegisterCallback<GeometryChangedEvent, ScrollView>(ApplyContentHeightAsFlexBasis, scrollView);
        }

        // 中身が伸びても縮んでも basis を中身の高さへ合わせ直す
        // Re-fit the basis to the content height whether it grows or shrinks
        private static void ApplyContentHeightAsFlexBasis(GeometryChangedEvent _, ScrollView scrollView)
        {
            var style = scrollView.resolvedStyle;
            var frame = style.paddingTop + style.paddingBottom + style.borderTopWidth + style.borderBottomWidth;
            var viewportStyle = scrollView.contentViewport.resolvedStyle;
            var basis = scrollView.contentContainer.layout.height + frame + viewportStyle.paddingTop + viewportStyle.paddingBottom;
            if (Mathf.Approximately(style.flexBasis.value, basis)) return;
            scrollView.style.flexBasis = basis;
        }
    }
}
