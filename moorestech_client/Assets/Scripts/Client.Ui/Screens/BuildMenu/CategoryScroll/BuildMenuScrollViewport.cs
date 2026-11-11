using System;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Motion.Playback;
using Client.Ui.Foundation.Motion.Steps;
using Client.Ui.Foundation.Motion.Tween;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.BuildMenu.Parts;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BuildMenu.CategoryScroll
{
    /// <summary>
    /// GameScrollView の上の IBuildMenuScrollViewport（表示 1 回につき 1 つ。隠すときに凍らせる）
    /// IBuildMenuScrollViewport over a GameScrollView (one per showing; frozen when hidden)
    /// </summary>
    public sealed class BuildMenuScrollViewport : IBuildMenuScrollViewport
    {
        // Web の scrollTo({ behavior: "smooth" }) の代わり。長さは USS のつまみ、緩急は C#（前例 ToastHostView。LX-71。D-10）
        // Stands in for the Web scrollTo({ behavior: "smooth" }); duration is a USS knob, easing stays in C# (precedent ToastHostView; LX-71, D-10)
        private const string SmoothScrollDurationProperty = "--build-menu-smooth-scroll-duration";

        private readonly GameScrollView _scroll;
        private readonly BuildMenuCategoryList _list;
        private readonly BuildMenuCategoryScrollTracker _tracker;
        private readonly CompositeDisposable _subscriptions = new();
        private UiMotionHandle _motion;
        private bool _forwarding;
        private bool _frozen;
        private float _frozenScrollTop;

        public BuildMenuScrollViewport(GameScrollView scroll, BuildMenuCategoryList list, BuildMenuCategoryScrollTracker tracker)
        {
            _scroll = scroll;
            _list = list;
            _tracker = tracker;

            // 位置は最初の復元の後だけ流す。復元前に ScrollView が流す 0 で保存位置を潰さない（L-3。D-8）
            // Forward offsets only after the first restore so the ScrollView's pre-restore 0 never flattens the saved position (L-3, D-8)
            scroll.OnScrollOffsetChanged.Where(_ => _forwarding).Subscribe(tracker.OnScrolled).AddTo(_subscriptions);

            // 中身・視口・スペーサの寸法が変わったらトラッカーへ（L-1。D-9）
            // Content, viewport and spacer size changes go to the tracker (L-1, D-9)
            scroll.OnContentLayoutChanged.Subscribe(_ => tracker.OnLayoutChanged()).AddTo(_subscriptions);
            list.Spacer.RegisterCallback<GeometryChangedEvent>(OnSpacerGeometryChanged);

            // 各カテゴリ群の寸法の変化もトラッカーへ（Web useBuildMenuCategoryScroll の群ごとの監視。D-9）
            // Each category group's size change also goes to the tracker (the Web per-group observation; D-9)
            list.OnGroupGeometryChanged.Subscribe(_ => tracker.OnLayoutChanged()).AddTo(_subscriptions);

            // ホイール・押下・キーはジャンプの固定を解く（Web の wheel・pointerdown・keydown。D-11）
            // Wheel, press and key input release the jump pin (the Web wheel, pointerdown and keydown; D-11)
            scroll.RegisterCallback<WheelEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            scroll.RegisterCallback<PointerDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            scroll.RegisterCallback<KeyDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
        }

        public float ScrollTop => _frozen ? _frozenScrollTop : _scroll.scrollOffset.y;
        public float ClientHeight => _scroll.GetViewportHeight();

        // 初回レイアウト前の NaN も閉じた後の 0 も「未レイアウト」（NaN との比較は偽）
        // Both the pre-layout NaN and the post-hide 0 count as not laid out (comparisons with NaN are false)
        private bool IsViewportLaidOut => ClientHeight > 0f;
        public float ScrollHeight => _scroll.GetContentHeight();

        // レイアウト前の NaN は「まだ当たっていない」を 0 で表す（トラッカーは要求値と比べて反映待ちを続ける）
        // A pre-layout NaN means "not applied yet", expressed as 0 (the tracker compares it with the request and keeps waiting)
        public float AppliedSpacerHeight => float.IsNaN(_list.Spacer.layout.height) ? 0f : _list.Spacer.layout.height;

        // 今の表示でまだ視口がレイアウトされていない間（閉じた後の 0 の高さを含む）は測れないとする。0 を実測と取り違えると復元位置を 0 へ潰す
        // Unmeasurable until the viewport is laid out in this showing (including the 0 height left from hiding); mistaking 0 for a measurement flattens the restore target
        public bool TryGetHeadingTop(Guid categoryGuid, out float top)
        {
            top = 0f;
            return IsViewportLaidOut && _list.TryGetHeading(categoryGuid, out var heading) && !float.IsNaN(heading.layout.height) && _scroll.TryGetContentTop(heading, out top);
        }

        public bool TryGetGroupHeight(Guid categoryGuid, out float height)
        {
            height = 0f;
            if (!IsViewportLaidOut || !_list.TryGetGroup(categoryGuid, out var group) || float.IsNaN(group.layout.height)) return false;
            height = group.layout.height;
            return true;
        }

        public void SetScrollTopImmediately(float scrollTop)
        {
            if (WarnIfFrozen(nameof(SetScrollTopImmediately))) return;
            _motion?.Cancel();
            _scroll.SetScrollOffset(scrollTop);
            _forwarding = true;
        }

        public void ScrollSmoothlyTo(float scrollTop)
        {
            if (WarnIfFrozen(nameof(ScrollSmoothlyTo))) return;
            _motion?.Cancel();
            // 長さが読めなければ（UssCustomStyleReader が LogError を出す）動きを付けずに飛ぶ。位置は Web と同じに着く
            // If the duration is unreadable (UssCustomStyleReader logs an error), jump without motion; the position still lands as on the Web
            if (!UssCustomStyleReader.TryReadMilliseconds(_scroll.customStyle, SmoothScrollDurationProperty, out var durationMs))
            {
                SetScrollTopImmediately(scrollTop);
                return;
            }
            var from = _scroll.scrollOffset;
            var spec = new UiMotionSpec(durationMs, 0f, UiEasing.EaseOutCubic, UiMotionLoop.Once);
            _motion = UiMotionPlayer.Play(_scroll, new UiTweenAnimation(spec, new ScrollOffsetTweenStep(_scroll, from, new Vector2(from.x, scrollTop))));
        }

        // 隠す直前に位置を写し取り、以後は ScrollView から何も受けない（D-7）
        // Snapshot the position right before hiding and take nothing from the ScrollView afterwards (D-7)
        public void Freeze()
        {
            _frozenScrollTop = _scroll.scrollOffset.y;
            _frozen = true;
            _forwarding = false;
            _motion?.Cancel();
            _subscriptions.Dispose();
            _list.Spacer.UnregisterCallback<GeometryChangedEvent>(OnSpacerGeometryChanged);
            _scroll.UnregisterCallback<WheelEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<PointerDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
            _scroll.UnregisterCallback<KeyDownEvent>(OnUserIntervened, TrickleDown.TrickleDown);
        }

        private void OnSpacerGeometryChanged(GeometryChangedEvent _)
        {
            _tracker.OnLayoutChanged();
        }

        private void OnUserIntervened(EventBase _)
        {
            _motion?.Cancel();
            _tracker.OnUserIntervened();
        }

        // 閉じた後の要求は無視し、理由を開発者ログへ出す（Presenter は閉じている間 View を描かないので通常は来ない）
        // Requests after closing are ignored with a developer log (the presenter does not render while closed, so normally none arrive)
        private bool WarnIfFrozen(string operation)
        {
            if (!_frozen) return false;
            Debug.LogWarning($"[BuildMenuScrollViewport] {operation} ignored: the viewport was frozen when the menu closed");
            return true;
        }
    }
}
