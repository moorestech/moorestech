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
    /// スクロール領域（縦は重ね・横は流れにバー）
    /// Scroll area (vertical overlays the bar, horizontal places it in flow)
    /// </summary>
    /// <remarks>
    /// 縦は Mantine ScrollArea の写し（A-35）、横は Web の overflow-x: auto の写し。SetScrollOffset・高さ・上端・中身の寸法の通知はスクロールスパイの縦の口で、横で呼ぶと LogError を出して何もしない
    /// Vertical copies Mantine ScrollArea (A-35) and horizontal copies the Web overflow-x: auto; SetScrollOffset, heights, content top and the layout notice are the scroll spy's vertical ports, and calling them on a horizontal view logs an error and does nothing
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
        public IObservable<Unit> OnContentLayoutChanged
        {
            get
            {
                RejectWhenHorizontal(nameof(OnContentLayoutChanged));
                return _onContentLayoutChanged;
            }
        }

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

            // スクロール量の変化は軸のバーの値で拾う
            // Offset changes come from the axis bar value
            AxisScroller().valueChanged += offset => _onScrollOffsetChanged.OnNext(offset);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());

            // 縦だけ中身と視口の寸法の変化をスクロールスパイへ知らせる（スクロールでも事象が来る場合に備え、高さの比較で重複を落とす）
            // Vertical only: tell the scroll spy about content and viewport size changes (heights are compared to drop duplicates in case scrolling also raises the event)
            if (axis == GameScrollAxis.Vertical)
            {
                contentContainer.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());
                contentViewport.RegisterCallback<GeometryChangedEvent>(_ => PublishWhenHeightsChanged());
            }

            #region Internal

            void UseOnlyHorizontalWheelDelta()
            {
                // Web の overflow-x: auto は縦ホイールで横へ送らない。Unity の既定は縦量を横送りに変え横量を捨てるので、その送りを 0 にして横量だけ自前で送る
                // The Web overflow-x: auto does not scroll sideways on a vertical wheel; Unity turns the vertical delta into sideways scroll and drops the horizontal one, so zero that and scroll by the horizontal delta here
                mouseWheelScrollSize = 0f;
                RegisterCallback<WheelEvent>(ScrollByHorizontalWheelDelta);
            }

            void ScrollByHorizontalWheelDelta(WheelEvent evt)
            {
                // Shift+縦ホイールは横量として扱う（Chromium の Shift+ホイールの横送り。横量が来る環境ではそちらを使う）
                // Treat Shift+vertical wheel as a horizontal amount (Chromium's Shift+wheel; a real horizontal delta wins where one arrives)
                var horizontalDelta = evt.shiftKey && evt.delta.x == 0f ? evt.delta.y : evt.delta.x;

                // 横量 1 ノッチを Web の 1 ノッチの px（A-71）で送る。動いたときだけ祖先へ渡さない（縦量は祖先のスクロールへ抜ける）
                // Scroll one horizontal notch by the Web pixels per notch (A-71); stop propagation only when it moved (vertical deltas pass on to ancestor scrolling)
                var before = horizontalScroller.value;
                horizontalScroller.value = before + horizontalDelta * WebWheelScale.PixelsPerNotch / WheelEvent.scrollDeltaPerTick;
                if (!Mathf.Approximately(before, horizontalScroller.value)) evt.StopPropagation();
            }

            void UpdateScrollerVisibility()
            {
                // 中身が見切れる軸だけバーを出す
                // Show the bar only on the overflowing axis
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
            if (RejectWhenHorizontal(nameof(SetScrollOffset))) return;
            scrollOffset = new Vector2(scrollOffset.x, y);
        }

        public float GetViewportHeight()
        {
            if (RejectWhenHorizontal(nameof(GetViewportHeight))) return 0f;
            return contentViewport.layout.height;
        }

        public float GetContentHeight()
        {
            if (RejectWhenHorizontal(nameof(GetContentHeight))) return 0f;
            return contentContainer.layout.height;
        }

        // 中身の上端からの距離。スクロールの平行移動は両者に同じく掛かるので打ち消し合う
        // Distance from the content top; the scroll translation applies to both and cancels out
        public bool TryGetContentTop(VisualElement descendant, out float top)
        {
            if (RejectWhenHorizontal(nameof(TryGetContentTop)))
            {
                top = 0f;
                return false;
            }
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

        // 縦専用の口を横で呼んだ誤用を知らせて拒む（fail-closed）
        // Report and refuse a vertical-only port called on a horizontal view (fail-closed)
        private bool RejectWhenHorizontal(string portName)
        {
            if (_axis == GameScrollAxis.Vertical) return false;
            Debug.LogError($"[GameScrollView] {name}: {portName} is vertical-only");
            return true;
        }

        private Scroller AxisScroller()
        {
            return _axis == GameScrollAxis.Horizontal ? horizontalScroller : verticalScroller;
        }

        private void OnCustomStyleResolved()
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
