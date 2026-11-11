// moorestech_client/Assets/Scripts/Client.Ui/Components/Controls/GameScrollView/GameScrollView.cs
using System;
using Client.Ui.Foundation.Input;
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

        // バーの内側余白＝幅×比（既定 0.2 は Mantine ScrollArea の size / 5。一覧は Web と同じく 0 に上書きする）
        // Bar padding = size x ratio (the default 0.2 is Mantine ScrollArea's size / 5; the item list overrides it to 0 as on the Web)
        private const string ScrollbarPaddingRatioProperty = "--game-scroll-view-scrollbar-padding-ratio-resolved";

        private readonly Subject<float> _onScrollOffsetChanged = new();

        private readonly Subject<Unit> _onContentLayoutChanged = new();
        private float _publishedContentHeight = float.NaN;
        private float _publishedViewportHeight = float.NaN;

        public IObservable<float> OnScrollOffsetChanged => _onScrollOffsetChanged;
        public IObservable<Unit> OnContentLayoutChanged => _onContentLayoutChanged;

        public GameScrollView(string testid) : base(ScrollViewMode.Vertical)
        {
            name = testid;
            AddToClassList("game-scroll-view");
            horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            // Web（CEF）の 1 ノッチの px（A-71）を UITK の WheelEvent の行へ直す
            // Convert the Web (CEF) pixels per notch (A-71) into UITK WheelEvent lines
            mouseWheelScrollSize = WebWheelScale.PixelsPerNotch / WheelEvent.scrollDeltaPerTick;

            // バーの有無は流れの高さで決める。Auto は子の変形（押下の 1px 沈み）まで数えて押す間だけバーを出す
            // Bar presence follows the flow heights; Auto counts child transforms (the 1px press sink) and flashes the bar while pressed
            verticalScrollerVisibility = ScrollerVisibility.Hidden;
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateVerticalScrollerVisibility());
            contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateVerticalScrollerVisibility());

            // スクロール量の変化はバーの値の変化で拾う（ホイール・ドラッグ・ScrollTo・SetScrollOffset のどれでも来る）
            // Offset changes come from the bar value (wheel, drag, ScrollTo and SetScrollOffset all move it)
            verticalScroller.valueChanged += offset => _onScrollOffsetChanged.OnNext(offset);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());

            // 中身と視口の寸法の変化をスクロールスパイへ知らせる（スクロールでも事象が来る場合に備え、高さの比較で重複を落とす）
            // Tell the scroll spy about content and viewport size changes (heights are compared to drop duplicates in case scrolling also raises the event)
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());
            contentViewport.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());

            #region Internal

            void UpdateVerticalScrollerVisibility()
            {
                var overflows = contentViewport.layout.height < contentContainer.layout.height;
                verticalScrollerVisibility = overflows ? ScrollerVisibility.AlwaysVisible : ScrollerVisibility.Hidden;
            }

            void PublishWhenHeightsChanged()
            {
                var contentHeight = GetContentHeight();
                var viewportHeight = GetViewportHeight();
                if (Mathf.Approximately(contentHeight, _publishedContentHeight) && Mathf.Approximately(viewportHeight, _publishedViewportHeight)) return;
                _publishedContentHeight = contentHeight;
                _publishedViewportHeight = viewportHeight;
                _onContentLayoutChanged.OnNext(Unit.Default);
            }

            #endregion
        }

        public void SetScrollOffset(float y)
        {
            scrollOffset = new Vector2(scrollOffset.x, y);
        }

        public float GetViewportHeight()
        {
            return contentViewport.layout.height;
        }

        public float GetContentHeight()
        {
            return contentContainer.layout.height;
        }

        // 中身の上端からの距離。スクロールの平行移動は両者に同じく掛かるので打ち消し合う
        // Distance from the content top; the scroll translation applies to both and cancels out
        public bool TryGetContentTop(VisualElement descendant, out float top)
        {
            // 中身の外の要素は基準が違う。取り違えを黙って 0 にせず知らせる
            // An element outside the content has another origin; report the mix-up instead of silently returning 0
            if (!contentContainer.Contains(descendant))
            {
                Debug.LogError($"[GameScrollView] {name}: {descendant.name} is not inside the content");
                top = 0f;
                return false;
            }
            top = descendant.ChangeCoordinatesTo(contentContainer, Vector2.zero).y;
            return true;
        }

        private void OnCustomStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出し、バーは Theme.uss の既定の幅のまま
            // When unreadable, UssCustomStyleReader logs an error and the bar keeps the Theme.uss default width
            if (!UssCustomStyleReader.TryReadPixels(customStyle, ScrollbarSizeProperty, out var size)) return;
            // 比も読めなければ UssCustomStyleReader が LogError を出し、バーは前の幅と余白のまま
            // If the ratio is unreadable too, UssCustomStyleReader logs an error and the bar keeps its previous width and padding
            if (!UssCustomStyleReader.TryReadNumber(customStyle, ScrollbarPaddingRatioProperty, out var ratio)) return;

            // 内側の余白はレイアウトで整数 px に丸まる（look-exception: LX-42）
            // The padding snaps to whole px in layout (look-exception: LX-42)
            var padding = size * ratio;
            verticalScroller.style.width = size;
            verticalScroller.style.paddingLeft = padding;
            verticalScroller.style.paddingRight = padding;
            verticalScroller.style.paddingTop = padding;
            verticalScroller.style.paddingBottom = padding;
        }
    }
}
