// moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs
using System;
using Client.Ui.Foundation.Theme;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 縦スクロール領域。バーは本文に重なる
    /// Vertical scroll area; the bar overlays the body
    /// </summary>
    public sealed class GameScrollView : ScrollView
    {
        private const string ScrollbarSizeProperty = "--game-scroll-view-scrollbar-size";

        // Mantine ScrollArea のバーは padding = size / 5 で、つまみは size − 2 × padding
        // Mantine ScrollArea pads the bar by size / 5, so the thumb is size - 2 x padding
        private const float ScrollbarPaddingRatio = 0.2f;

        // Web（CEF）はホイール 1 ノッチで本文を 60px 動かす（CefUnity の ScrollInputPipeline.WheelPixelsPerStep）。UITK は 1 ノッチを WheelEvent の scrollDeltaPerTick 行で届ける
        // The Web (CEF) scrolls 60px per wheel notch (CefUnity ScrollInputPipeline.WheelPixelsPerStep); UITK delivers one notch as scrollDeltaPerTick lines of WheelEvent
        private const float WebWheelPixelsPerNotch = 60f;

        private readonly Subject<float> _onScrollOffsetChanged = new();

        public IObservable<float> OnScrollOffsetChanged => _onScrollOffsetChanged;

        public GameScrollView(string testid) : base(ScrollViewMode.Vertical)
        {
            name = testid;
            AddToClassList("game-scroll-view");
            horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            mouseWheelScrollSize = WebWheelPixelsPerNotch / WheelEvent.scrollDeltaPerTick;

            // バーの有無は流れの高さで決める。Auto は子の変形（押下の 1px 沈み）まで数えて押す間だけバーを出す
            // Bar presence follows the flow heights; Auto counts child transforms (the 1px press sink) and flashes the bar while pressed
            verticalScrollerVisibility = ScrollerVisibility.Hidden;
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateVerticalScrollerVisibility());
            contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateVerticalScrollerVisibility());

            // スクロール量の変化はバーの値の変化で拾う（ホイール・ドラッグ・ScrollTo・SetScrollOffset のどれでも来る）
            // Offset changes come from the bar value (wheel, drag, ScrollTo and SetScrollOffset all move it)
            verticalScroller.valueChanged += offset => _onScrollOffsetChanged.OnNext(offset);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());

            #region Internal

            void UpdateVerticalScrollerVisibility()
            {
                var overflows = contentViewport.layout.height < contentContainer.layout.height;
                verticalScrollerVisibility = overflows ? ScrollerVisibility.AlwaysVisible : ScrollerVisibility.Hidden;
            }

            #endregion
        }

        public void SetScrollOffset(float y)
        {
            scrollOffset = new Vector2(scrollOffset.x, y);
        }

        private void OnCustomStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出し、バーは Theme.uss の既定の幅のまま
            // When unreadable, UssCustomStyleReader logs an error and the bar keeps the Theme.uss default width
            if (!UssCustomStyleReader.TryReadPixels(customStyle, ScrollbarSizeProperty, out var size)) return;
            // 内側の余白はレイアウトで整数 px に丸まる（look-exception: LX-42）
            // The padding snaps to whole px in layout (look-exception: LX-42)
            var padding = size * ScrollbarPaddingRatio;
            verticalScroller.style.width = size;
            verticalScroller.style.paddingLeft = padding;
            verticalScroller.style.paddingRight = padding;
            verticalScroller.style.paddingTop = padding;
            verticalScroller.style.paddingBottom = padding;
        }
    }
}
