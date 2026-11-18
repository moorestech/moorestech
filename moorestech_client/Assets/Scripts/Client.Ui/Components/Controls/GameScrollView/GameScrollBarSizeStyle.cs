using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// USS のつまみからスクロールバーの太さと内側余白を当てる
    /// Applies the scrollbar thickness and inner padding from the USS knobs
    /// </summary>
    internal static class GameScrollBarSizeStyle
    {
        private const string ScrollbarSizeProperty = "--game-scroll-view-scrollbar-size";

        // バーの内側余白＝太さ×比（既定 0.2 は Mantine ScrollArea の size / 5。一覧は Web と同じく 0 に上書きする）
        // Bar padding = size x ratio (the default 0.2 is Mantine ScrollArea's size / 5; the item list overrides it to 0 as on the Web)
        private const string ScrollbarPaddingRatioProperty = "--game-scroll-view-scrollbar-padding-ratio-resolved";

        public static void Apply(ICustomStyle customStyle, Scroller bar, GameScrollAxis axis)
        {
            // 読めないとき LogError、既定の太さのまま
            // When unreadable, LogError and keep the default thickness
            if (!UssCustomStyleReader.TryReadPixels(customStyle, ScrollbarSizeProperty, out var size)) return;
            // 比も読めなければ LogError、前の値のまま
            // If the ratio is unreadable, LogError and keep the previous value
            if (!UssCustomStyleReader.TryReadNumber(customStyle, ScrollbarPaddingRatioProperty, out var ratio)) return;

            // 太さは交差の軸（縦は幅・横は高さ）。内側の余白はレイアウトで整数 px に丸まる（look-exception: LX-42）
            // Thickness runs across the axis (width for vertical, height for horizontal); the padding snaps to whole px in layout (look-exception: LX-42)
            var padding = size * ratio;
            if (axis == GameScrollAxis.Horizontal) bar.style.height = size;
            else bar.style.width = size;
            bar.style.paddingLeft = padding;
            bar.style.paddingRight = padding;
            bar.style.paddingTop = padding;
            bar.style.paddingBottom = padding;
        }
    }
}
