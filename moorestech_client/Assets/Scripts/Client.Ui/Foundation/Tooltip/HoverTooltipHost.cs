using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Tooltip
{
    /// <summary>
    /// ホバーのツールチップの宿主（Tooltip 層に 1 つ）
    /// Hover tooltip host (one in the Tooltip layer)
    /// </summary>
    public sealed class HoverTooltipHost
    {
        private const string ShownClassName = "hover-tooltip-host--shown";

        private readonly IHoverTooltipFace _face;
        private readonly StageScaleSource _stageScale;
        private readonly VisualElement _anchor = new() { name = "hover-tooltip-host", pickingMode = PickingMode.Ignore };
        private readonly HoverTooltipScrollWatch _scrollWatch = new();
        private VisualElement _layer;

        // ポインタの下の対象（印の有無を問わない）。捕捉中・パネルの外・見張りの印が外れた後は null
        // The element under the pointer (marked or not); null while captured, outside the panel, or after the tracked mark detached
        private VisualElement _pointed;

        // 出している印。_pointed から Reevaluate でだけ引き直し、取り外しの見張りを張る
        // The shown mark; re-derived from _pointed only in Reevaluate, and the target of the detach watch
        private VisualElement _tracked;

        // スクロールの引っ込め。座標が実際に変わる移動でだけ解き、印の付け外しでは解かない（Web の dismissed）
        // Scroll retraction; cleared only by a real coordinate change, never by mark changes (Web dismissed)
        private bool _dismissed;
        private Vector2 _lastPointer = new(float.NaN, float.NaN);

        // 顔へ渡した最後の文。同じ文の参照なら作り直さない（ポインタ移動ごとの再構築を避ける）
        // The last text handed to the face; an identical reference is not rebuilt (avoids rebuilding on every pointer move)
        private UiText _shownText;

        public HoverTooltipHost(IHoverTooltipFace face, StageScaleSource stageScale)
        {
            _face = face;
            _stageScale = stageScale;
            _anchor.AddToClassList("hover-tooltip-host");
            _face.AttachTo(_anchor);
            _scrollWatch.OnAncestorScrolled.Subscribe(_ => Dismiss());
            HoverTooltipMark.OnMarkChanged.Subscribe(_ => Reevaluate());
            // 倍率だけの変化（16:9 どうし）では GeometryChangedEvent が来ないので、倍率の変化でも置き直す
            // A scale-only change (16:9 to 16:9) raises no GeometryChangedEvent, so reposition on scale changes too
            _stageScale.Scale.Subscribe(_ => Reposition());
            _anchor.RegisterCallback<GeometryChangedEvent>(_ => Reposition());
        }

        public void Attach(VisualElement pointerRoot, VisualElement layer)
        {
            // 空き地へ出た移動は根の PointerLeaveEvent で受ける（空き地の中の移動は届かない。台帳の実測）。移動と捕捉の始まりは根で TrickleDown に受ける
            // A move out to empty space arrives as the root PointerLeaveEvent (moves inside empty space never arrive; ledger measurement); moves and capture starts are received at the root with TrickleDown
            _layer = layer;
            layer.Add(_anchor);
            pointerRoot.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            pointerRoot.RegisterCallback<PointerCaptureEvent>(_ => ForgetPointed(), TrickleDown.TrickleDown);
            pointerRoot.RegisterCallback<PointerLeaveEvent>(_ => ForgetPointed());
            // 捕捉の終わりで最後の座標から対象を引き直す（離上後は移動が来ず、動かすまでツールチップが戻らないため）
            // Re-pick from the last coordinate when capture ends (no move follows a release, so the tooltip would stay hidden until the pointer moves)
            pointerRoot.RegisterCallback<PointerCaptureOutEvent>(_ => RepickAfterCapture(), TrickleDown.TrickleDown);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            // 座標が実際に変わった移動だけが引っ込めを解く（スクロール中の座標据え置きの移動で開き直さない）
            // Only a real coordinate change clears the retraction (a stationary move during a scroll never reopens)
            Vector2 pointer = evt.position;
            if (pointer != _lastPointer) _dismissed = false;
            _lastPointer = pointer;

            // 捕捉中（押下・ドラッグ中）は対象を持たない。捕捉中も下の要素へ進入が届きうるので（台帳の実測）、進入ではなく捕捉で決める
            // While captured (press or drag) there is no target; entries can still reach elements underneath (ledger), so decide by capture
            var panel = _layer.panel;
            Point(panel.GetCapturingElement(evt.pointerId) == null ? panel.Pick(pointer) : null);
        }

        private void RepickAfterCapture()
        {
            // 座標がまだ無ければ対象なしのまま。_dismissed は触らない（スクロールの引っ込めを捕捉で解かない）
            // With no coordinate yet there is no target; _dismissed stays untouched (capture must not clear a scroll retraction)
            var hasPointer = !float.IsNaN(_lastPointer.x);
            Point(hasPointer ? _layer.panel.Pick(_lastPointer) : null);
        }

        private void ForgetPointed()
        {
            Point(null);
        }

        private void Point(VisualElement pointed)
        {
            // スクロールの見張りは印の有無と独立にポインタの下の対象の祖先へ張る。印の無い間のスクロールも引っ込めに数える（Web は disabled 中もホバーで購読する）
            // Watch scrolls on the pointed element's ancestors regardless of marks, so a scroll while unmarked still retracts (the Web subscribes by hover even while disabled)
            if (pointed != _pointed)
            {
                // 取り外しの見張りは _pointed にも張る（_tracked は _pointed 自身か祖先なので、子だけが外れる場合も拾える）
                // Watch _pointed for detach too (_tracked is _pointed or an ancestor, so a child-only detach is caught)
                _pointed?.UnregisterCallback<DetachFromPanelEvent>(OnPointedDetached);
                _pointed = pointed;
                _pointed?.RegisterCallback<DetachFromPanelEvent>(OnPointedDetached);
                if (_pointed == null) _scrollWatch.Clear();
                else _scrollWatch.Watch(_pointed);
            }
            Reevaluate();
        }

        private void Reevaluate()
        {
            // ポインタの下の対象から印を引き直す。印の付け外しもここを通るので、静止したまま付いた印も出る（Web の disabled の切替と同じ見え方）
            // Re-derive the mark from the element under the pointer; mark changes come here too, so a mark added under a resting pointer shows (as Web toggles disabled)
            // silent-ok: 対象が無い・木から外れた・印が無いのはホバーしていない正常な状態 / HoverTooltipHostTest と HoverTooltipHostChangeTest が出し入れを固定する
            if (_pointed == null || _pointed.panel == null || !HoverTooltipMark.TryFindMarked(_pointed, out var marked, out var text))
            {
                Track(null);
                _shownText = null;
                Refresh();
                return;
            }
            Track(marked);
            if (!ReferenceEquals(text, _shownText))
            {
                _shownText = text;
                _face.SetText(text);
            }
            Refresh();
        }

        private void Track(VisualElement marked)
        {
            // 取り外しの見張りは出している印の 1 つだけに張る（スクロールの見張りは Point がポインタの下の対象に張る）
            // Watch only the shown mark for removal from the tree (Point watches scrolls on the element under the pointer)
            if (marked == _tracked) return;
            _tracked?.UnregisterCallback<DetachFromPanelEvent>(OnTrackedDetached);
            _tracked = marked;
            _tracked?.RegisterCallback<DetachFromPanelEvent>(OnTrackedDetached);
        }

        private void OnTrackedDetached(DetachFromPanelEvent evt)
        {
            ForgetPointed();
        }

        private void OnPointedDetached(DetachFromPanelEvent evt)
        {
            ForgetPointed();
        }

        private void Dismiss()
        {
            if (_dismissed) return;
            _dismissed = true;
            Refresh();
        }

        private void Refresh()
        {
            _anchor.EnableInClassList(ShownClassName, _tracked != null && !_dismissed);
            Reposition();
        }

        private void Reposition()
        {
            // silent-ok: 隠れている間と最初のレイアウト前は置かない / 顔の寸法が決まると GeometryChangedEvent で呼び直され、HoverTooltipHostTest が出し入れを固定する
            if (_tracked == null || float.IsNaN(_anchor.layout.width)) return;

            // 間隔と横の押し戻しの余白は Web と同じ実画面 px を、配られた舞台の倍率でパネル px に直す
            // Convert the Web's real-screen px offset and shift padding into panel px with the handed-in stage scale
            var scale = _stageScale.Scale.Value;
            var target = _layer.WorldToLocal(_tracked.worldBound);
            var faceSize = new Vector2(_anchor.layout.width, _anchor.layout.height);
            var position = HoverTooltipPlacement.Place(target, faceSize, _layer.contentRect, HoverTooltipPlacement.OffsetScreenPx / scale, HoverTooltipPlacement.ShiftPaddingScreenPx / scale);
            _anchor.style.left = position.x;
            _anchor.style.top = position.y;
        }
    }
}
