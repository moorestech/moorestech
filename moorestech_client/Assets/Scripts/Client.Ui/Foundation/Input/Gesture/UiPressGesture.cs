using Client.Ui.Foundation.Stage.ScaleReading;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input.Gesture
{
    /// <summary>
    /// 押下をタップ・ドラッグ・中断に分ける
    /// Splits a press into tap, drag, or cancel
    /// </summary>
    public sealed class UiPressGesture
    {
        private const int PrimaryButton = 0;

        private readonly VisualElement _element;
        private readonly IUiPressGestureHandler _handler;
        private LivePress _press;

        public UiPressGesture(VisualElement element, IUiPressGestureHandler handler)
        {
            _element = element;
            _handler = handler;
            element.RegisterCallback<PointerDownEvent>(OnPointerDown);
            element.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            element.RegisterCallback<PointerUpEvent>(OnPointerUp);
            element.RegisterCallback<PointerCancelEvent>(evt => Abort(evt.pointerId));
            element.RegisterCallback<PointerCaptureOutEvent>(evt => Abort(evt.pointerId));
            // 捕捉中に要素が外れると PointerCaptureOutEvent が届かないので、外れも中断として扱う
            // Detaching while captured raises no PointerCaptureOutEvent, so treat the detach as an abort too
            element.RegisterCallback<DetachFromPanelEvent>(_ => AbortLivePress());
        }

        // Web の clientX は実画面 px なので、パネル px の移動量に倍率を掛けて同じ 5px で比べる
        // The Web clientX is real-screen px, so multiply the panel-px delta by the scale and compare with the same 5px
        internal static bool ExceedsOnScreen(Vector2 panelDelta, float stageScale)
        {
            return UiDragThreshold.Exceeded(panelDelta.x * stageScale, panelDelta.y * stageScale);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            // silent-ok: 左以外・主でないポインタは Web と同じく受けない（右クリックの削除を温存） / L1「右ボタンの押下は無視し捕捉もしない」が固定する
            if (!evt.isPrimary || evt.button != PrimaryButton) return;
            // silent-ok: 進行中の押下を第 2 ポインタに乗っ取らせない（Web と同じ。乗っ取ると解放が届かず掴みが固着する） / マウスは 1 本で L1 では作れないので、レビューで見る
            if (_press != null) return;

            // 押下を受けたら捕捉し、終わりの事象を取りこぼさない
            // Capture on an accepted press so the end events are never missed
            Vector2 position = evt.position;
            _element.CapturePointer(evt.pointerId);
            // 倍率は押下の時点で 1 回だけ読む（DI の外で new されるので StageScaleSource を受けない。D-2）
            // Read the scale once at press time (built outside DI, so it takes no StageScaleSource; D-2)
            var stageScale = StageScaleMeasure.ReadOrUnit(_element.panel, $"UiPressGesture {_element.name}");
            _press = new LivePress(evt.pointerId, position, evt.target as VisualElement, stageScale);

            // 押下の確定後に受け手を呼ぶ。受け手の中で要素が外れても通常の中断として片付く
            // Call the handler after the press is committed so a detach inside it ends as a normal abort
            _handler.OnPressStarted(position);
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            var live = _press;
            if (live == null || live.PointerId != evt.pointerId) return;
            Vector2 position = evt.position;

            // 他ボタンを押したままの主ボタン解放は PointerUp でなく chorded な PointerMove で届くので、ここでも押下を終える
            // Releasing the primary button while another is held arrives as a chorded PointerMove, not PointerUp, so end the press here too
            if (evt.button == PrimaryButton && (evt.pressedButtons & 1) == 0)
            {
                FinishPress(evt.pointerId, position);
                return;
            }

            // 閾値を超えた回で初めてドラッグにし、その回は押下点からの全量を渡して指へぴったり追わせる
            // Commit to a drag only past the threshold; that move hands the whole delta from the press point so content tracks the pointer
            if (!live.Dragged)
            {
                if (!ExceedsOnScreen(position - live.Start, live.StageScale)) return;
                live.Dragged = true;
            }
            var move = new UiPressMove(position - live.Start, position - live.Last);
            live.Last = position;
            _handler.OnDragMoved(move);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            // 主ボタン以外の解放は進行中の押下を終わらせない（終わらせるとタップが誤発火する）
            // A non-primary release never ends the live press (ending it would misfire a tap)
            if (evt.button != PrimaryButton) return;
            FinishPress(evt.pointerId, evt.position);
        }

        private void FinishPress(int pointerId, Vector2 position)
        {
            // 解放の終わり方は PointerUp と chorded PointerMove で共通（ドラッグならドロップ、でなければタップ）
            // Shared release ending for PointerUp and the chorded PointerMove (drop if dragged, else tap)
            var live = EndPress(pointerId);
            if (live == null) return;
            if (live.Dragged) _handler.OnDragEnded(position);
            else _handler.OnTapped(live.Target);
        }

        private void Abort(int pointerId)
        {
            // 取消・捕捉の喪失は中断で、タップにもドロップにもしない
            // Cancel or lost capture aborts; it becomes neither a tap nor a drop
            var live = EndPress(pointerId);
            if (live != null) _handler.OnAborted(live.Dragged);
        }

        private void AbortLivePress()
        {
            if (_press != null) Abort(_press.PointerId);
        }

        private LivePress EndPress(int pointerId)
        {
            if (_press == null || _press.PointerId != pointerId) return null;
            var live = _press;

            // 捕捉を外すと PointerCaptureOutEvent が来るので、先に押下を消して中断と取り違えない
            // Releasing capture raises PointerCaptureOutEvent, so clear the press first to avoid reading it as an abort
            _press = null;
            if (_element.HasPointerCapture(pointerId)) _element.ReleasePointer(pointerId);
            return live;
        }

        // 進行中の押下。パン量の基準（Last）とタップ判定の基準（Start）を併せ持つ
        // The live press, holding both the move baseline (Last) and the tap baseline (Start)
        private sealed class LivePress
        {
            public readonly int PointerId;
            public readonly Vector2 Start;
            public readonly VisualElement Target;
            public readonly float StageScale;
            public Vector2 Last;
            public bool Dragged;

            public LivePress(int pointerId, Vector2 start, VisualElement target, float stageScale)
            {
                PointerId = pointerId;
                Start = start;
                Target = target;
                StageScale = stageScale;
                Last = start;
            }
        }
    }
}
