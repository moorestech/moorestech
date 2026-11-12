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
    /// スクロール領域。縦はバーが本文に重なり、横はバーが本文の下の流れに入る
    /// Scroll area; the vertical bar overlays the body and the horizontal bar sits in the flow below it
    /// </summary>
    /// <remarks>
    /// 縦は Mantine ScrollArea の写し（A-35）、横は Web の overflow-x: auto の写し（チャレンジのカテゴリ列）。SetScrollOffset・高さ・上端はスクロールスパイの縦の口で、横では使わない
    /// Vertical copies Mantine ScrollArea (A-35) and horizontal copies the Web overflow-x: auto (challenge category row); SetScrollOffset, heights and content top are the scroll spy's vertical ports, unused for horizontal
    /// </remarks>
    public sealed class GameScrollView : ScrollView
    {
        private const string ScrollbarSizeProperty = "--game-scroll-view-scrollbar-size";

        // バーの内側余白＝太さ×比（既定 0.2 は Mantine ScrollArea の size / 5。一覧は Web と同じく 0 に上書きする）
        // Bar padding = size x ratio (the default 0.2 is Mantine ScrollArea's size / 5; the item list overrides it to 0 as on the Web)
        private const string ScrollbarPaddingRatioProperty = "--game-scroll-view-scrollbar-padding-ratio-resolved";

        private readonly GameScrollAxis _axis;
        private readonly Subject<float> _onScrollOffsetChanged = new();

        private readonly Subject<Unit> _onContentLayoutChanged = new();
        private float _publishedContentHeight = float.NaN;
        private float _publishedViewportHeight = float.NaN;

        public IObservable<float> OnScrollOffsetChanged => _onScrollOffsetChanged;
        public IObservable<Unit> OnContentLayoutChanged => _onContentLayoutChanged;

        public GameScrollView(string testid, GameScrollAxis axis) : base(axis == GameScrollAxis.Horizontal ? ScrollViewMode.Horizontal : ScrollViewMode.Vertical)
        {
            _axis = axis;
            name = testid;
            AddToClassList("game-scroll-view");
            AddToClassList(axis == GameScrollAxis.Horizontal ? "game-scroll-view--horizontal" : "game-scroll-view--vertical");
            // Web（CEF）の 1 ノッチの px（A-71）を UITK の WheelEvent の行へ直す
            // Convert the Web (CEF) pixels per notch (A-71) into UITK WheelEvent lines
            mouseWheelScrollSize = WebWheelScale.PixelsPerNotch / WheelEvent.scrollDeltaPerTick;
            if (axis == GameScrollAxis.Horizontal) UseOnlyHorizontalWheelDelta();

            // バーの有無は向きの軸の流れの寸法で決める。Auto は子の変形（押下の 1px 沈み）まで数えて押す間だけバーを出す
            // Bar presence follows the flow size along the axis; Auto counts child transforms (the 1px press sink) and flashes the bar while pressed
            horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            verticalScrollerVisibility = ScrollerVisibility.Hidden;
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollerVisibility());
            contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollerVisibility());

            // スクロール量の変化は向きの軸のバーの値の変化で拾う（ホイール・ドラッグ・ScrollTo・SetScrollOffset のどれでも来る）
            // Offset changes come from the axis bar value (wheel, drag, ScrollTo and SetScrollOffset all move it)
            AxisScroller().valueChanged += offset => _onScrollOffsetChanged.OnNext(offset);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());

            // 中身と視口の寸法の変化をスクロールスパイへ知らせる（スクロールでも事象が来る場合に備え、高さの比較で重複を落とす）
            // Tell the scroll spy about content and viewport size changes (heights are compared to drop duplicates in case scrolling also raises the event)
            contentContainer.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());
            contentViewport.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());

            #region Internal

            void UseOnlyHorizontalWheelDelta()
            {
                // Web の overflow-x: auto は縦ホイールで横へ送らない。Unity の既定は縦量を横送りに変え横量を捨てるので、その送りを 0 にして横量だけ自前で送る
                // The Web overflow-x: auto does not scroll sideways on a vertical wheel; Unity turns the vertical delta into sideways scroll and drops the horizontal one, so zero that and scroll by the horizontal delta here
                mouseWheelScrollSize = 0f;
                RegisterCallback<WheelEvent>(ScrollByHorizontalWheelDelta);
            }

            void UpdateScrollerVisibility()
            {
                // 向きの軸で中身が見える寸法を越えたときだけバーを出す。交差の軸のバーは常に出さない
                // Show the bar only when the content exceeds the visible size along the axis; the cross-axis bar never shows
                var overflows = _axis == GameScrollAxis.Horizontal
                    ? contentViewport.layout.width < contentContainer.layout.width
                    : contentViewport.layout.height < contentContainer.layout.height;
                var visibility = overflows ? ScrollerVisibility.AlwaysVisible : ScrollerVisibility.Hidden;
                if (_axis == GameScrollAxis.Horizontal) horizontalScrollerVisibility = visibility;
                else verticalScrollerVisibility = visibility;
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

        private void ScrollByHorizontalWheelDelta(WheelEvent evt)
        {
            // 横量 1 ノッチを Web の 1 ノッチの px（A-71）で送る。動いたときだけ祖先へ渡さない（縦量は祖先のスクロールへ抜ける）
            // Scroll one horizontal notch by the Web pixels per notch (A-71); stop propagation only when it moved (vertical deltas pass on to ancestor scrolling)
            var before = horizontalScroller.value;
            horizontalScroller.value = before + evt.delta.x * WebWheelScale.PixelsPerNotch / WheelEvent.scrollDeltaPerTick;
            if (!Mathf.Approximately(before, horizontalScroller.value)) evt.StopPropagation();
        }

        private Scroller AxisScroller()
        {
            return _axis == GameScrollAxis.Horizontal ? horizontalScroller : verticalScroller;
        }

        private void OnCustomStyleResolved()
        {
            // 読めないときは UssCustomStyleReader が LogError を出し、バーは Theme.uss の既定の太さのまま
            // When unreadable, UssCustomStyleReader logs an error and the bar keeps the Theme.uss default thickness
            if (!UssCustomStyleReader.TryReadPixels(customStyle, ScrollbarSizeProperty, out var size)) return;
            // 比も読めなければ UssCustomStyleReader が LogError を出し、バーは前の太さと余白のまま
            // If the ratio is unreadable too, UssCustomStyleReader logs an error and the bar keeps its previous thickness and padding
            if (!UssCustomStyleReader.TryReadNumber(customStyle, ScrollbarPaddingRatioProperty, out var ratio)) return;

            // 太さは交差の軸（縦は幅・横は高さ）。内側の余白はレイアウトで整数 px に丸まる（look-exception: LX-42）
            // Thickness runs across the axis (width for vertical, height for horizontal); the padding snaps to whole px in layout (look-exception: LX-42)
            var padding = size * ratio;
            var bar = AxisScroller();
            if (_axis == GameScrollAxis.Horizontal) bar.style.height = size;
            else bar.style.width = size;
            bar.style.paddingLeft = padding;
            bar.style.paddingRight = padding;
            bar.style.paddingTop = padding;
            bar.style.paddingBottom = padding;
        }
    }
}
