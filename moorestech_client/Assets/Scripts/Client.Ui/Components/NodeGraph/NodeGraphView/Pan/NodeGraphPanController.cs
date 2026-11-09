using Client.Ui.Foundation.Input.Gesture;
using Client.Ui.Foundation.Motion.Playback;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ビューポートの押下を分類してパン・タップ・滑走にする。web: moorestech_web/webui/src/shared/treeView/useTreePanGesture.ts と viewport/usePanInertia.ts
    /// Classifies viewport presses into pan, tap and glide; web: useTreePanGesture.ts and viewport/usePanInertia.ts
    /// </summary>
    /// <remarks>
    /// UiPressMove は時刻を持たないので、ポインタ事象の時刻を UiPressGesture より先に登録した受け手で記録して速度に使う（A-116）
    /// UiPressMove carries no time, so pointer event timestamps are recorded by callbacks registered before UiPressGesture and used for velocity (A-116)
    /// </remarks>
    internal sealed class NodeGraphPanController : IUiPressGestureHandler
    {
        private readonly NodeGraphView _view;
        private readonly NodeGraphCursorSwitcher _cursor;
        private readonly NodeGraphPanInertiaTracker _tracker = new();
        private UiMotionHandle _glide;
        private long _lastPointerTimestamp;
        private VisualElement _lastPointerDownTarget;
        private string _pressedNodeKey;

        public NodeGraphPanController(NodeGraphView view, NodeGraphCursorSwitcher cursor)
        {
            _view = view;
            _cursor = cursor;

            // 同じ要素の同じ相の受け手は登録順に走るので、記録と滑走の停止を先に登録する
            // Callbacks on the same element and phase run in registration order, so recording and glide stop go first
            view.RegisterCallback<PointerDownEvent>(OnAnyPointerDown);
            view.RegisterCallback<PointerMoveEvent>(evt => _lastPointerTimestamp = evt.timestamp);
            view.RegisterCallback<PointerUpEvent>(evt => _lastPointerTimestamp = evt.timestamp);
            new UiPressGesture(view, this);
        }

        public void CancelGlide()
        {
            // silent-ok: 滑走していなければ止めるものが無い / 正常系で知らせる相手はいない
            if (_glide == null) return;
            _glide.Cancel();
            _glide = null;
        }

        public void OnPressStarted(Vector2 panelPosition)
        {
            // 押した時点でノードのキーを覚える。離すまでに SetGraph で包みが外れても親を辿らずに済む
            // Remember the node key at press time so a wrapper removed by SetGraph before release needs no parent walk
            _tracker.Reset();
            _pressedNodeKey = _view.FindNodeKey(_lastPointerDownTarget);
        }

        public void OnDragMoved(UiPressMove move)
        {
            // パン中は握った手のカーソルとクラスにする（Web の :active の grabbing）
            // Show the grabbing cursor and class while panning (the Web :active grabbing)
            _cursor.StartPanning();

            // 座標はパネル px（＝ビューポートの局所の px）なので、そのまま速度とパンに使う
            // Coordinates are panel px (= viewport-local px), so they feed velocity and pan as-is
            _tracker.TrackMove(move.SinceLast, _lastPointerTimestamp);
            _view.PanBy(move.SinceLast);
        }

        public void OnDragEnded(Vector2 panelPosition)
        {
            _cursor.EndPanning(panelPosition);
            // silent-ok: 止まってから離した・遅い回は Web と同じく滑走しない / 正常系で知らせる相手はいない
            if (!_tracker.TryStartFling(_lastPointerTimestamp, out var velocity)) return;
            _glide = UiMotionPlayer.Play(_view, NodeGraphPanVelocity.CreateGlide(velocity.magnitude, new NodeGraphGlideStep(_view, velocity.normalized)));
        }

        public void OnTapped(VisualElement pressTarget)
        {
            _view.TapNode(_pressedNodeKey);
        }

        public void OnAborted(bool dragged)
        {
            // 取消・捕捉の喪失はドラッグ済みでも滑走させず、握った手を戻す
            // A cancel or lost capture never glides, even after dragging, and drops the grabbing cursor
            _cursor.AbortPanning();
            _tracker.Reset();
            CancelGlide();
        }

        private void OnAnyPointerDown(PointerDownEvent evt)
        {
            // 押下は受理の可否（右ボタン・第 2 ポインタ）によらず滑走を止める（Web の onPointerDown の包み）
            // Any press stops the glide whether or not it is accepted (right button, second pointer), like the Web onPointerDown wrapper
            _lastPointerTimestamp = evt.timestamp;
            _lastPointerDownTarget = evt.target as VisualElement;
            CancelGlide();
        }
    }
}
