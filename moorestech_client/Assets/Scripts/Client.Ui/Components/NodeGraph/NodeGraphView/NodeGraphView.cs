using System;
using Client.Ui.Foundation.Anchor;
using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Theme;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// パン・ズーム・慣性つきのノードグラフ（研究ツリー・チャレンジ一覧）。web: moorestech_web/webui/src/shared/treeView/TreeView.tsx
    /// Node graph with pan, zoom and inertia (research tree, challenge list); web: moorestech_web/webui/src/shared/treeView/TreeView.tsx
    /// </summary>
    /// <remarks>
    /// ビューポートは View の寿命で保たれる（Web の viewportStore がセッション内で保つのと同じ寿命）。一度でも視点が動いた後に非表示から戻すと、Web の復元と同じく初回の中央寄せと寸法の追従をしない。カードは押下を捕まえない・止めない（Button・Clickable を使わない）ので、カード上の押下もパンになり、タップは OnNodeTapped で受ける（Web TreeView.tsx と同じ）
    /// The viewport lives as long as the View (the same lifetime as the Web viewportStore within a session). Once the view has moved, coming back from hidden skips initial centering and resize-follow like the Web restore. Cards never capture or stop presses (do not use Button or Clickable), so a press on a card also pans and taps arrive on OnNodeTapped (same as the Web TreeView.tsx)
    /// </remarks>
    public sealed class NodeGraphView : VisualElement
    {
        private const string ClipInsetProperty = "--node-graph-clip-inset";

        private readonly VisualElement _canvas;
        private readonly NodeGraphScene _scene;
        private readonly NodeGraphPanController _pan;
        private readonly NodeGraphPointerFeedback _pointerFeedback;
        private readonly Subject<string> _onNodeTapped = new();
        private bool _viewportHeld;
        private bool _hasEmittedViewport;
        private NodeGraphViewport _viewport;
        private NodeGraphFocus _initialFocus = NodeGraphFocus.None;
        private bool _hasCentered;
        private bool _hasClipInset;
        private float _clipInset;
        private Vector2 _lastContentSize;

        public IObservable<string> OnNodeTapped => _onNodeTapped;

        public NodeGraphView(string testIdPrefix, IUiOsCursor osCursor)
        {
            // 根とキャンバスと線・ノードの場面を作る
            // Build the root, the canvas and the edge/node scene
            name = $"{testIdPrefix}-viewport";
            AddToClassList("node-graph");
            // USS の overflow:hidden で子を切るので、チュートリアルの枠を切る容器として印を付ける
            // The USS overflow:hidden clips children, so mark this as a clip container for tutorial anchors
            TutorialAnchor.MarkClipContainer(this);
            _canvas = new VisualElement { name = $"{testIdPrefix}-canvas" };
            _canvas.AddToClassList("node-graph__canvas");
            Add(_canvas);
            _scene = new NodeGraphScene(_canvas);
            _viewport = NodeGraphViewport.Identity;
            ApplyViewport();

            // 押下とパンは Foundation の UiPressGesture、手のカーソルは Foundation の OS カーソル、ホイール・寸法・つまみは自分で受ける
            // Press and pan use Foundation UiPressGesture, the hand cursor the Foundation OS cursor; wheel, size and knobs are handled here
            _pointerFeedback = new NodeGraphPointerFeedback(this, osCursor);
            _pan = new NodeGraphPanController(this, _pointerFeedback);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        public void SetGraph(NodeGraphContent content)
        {
            _scene.Rebuild(content.Nodes);
            _initialFocus = content.InitialFocus;
            CenterOnInitialFocusOnce();
        }

        internal void PanBy(Vector2 delta)
        {
            SetViewport(new NodeGraphViewport(_viewport.X + delta.x, _viewport.Y + delta.y, _viewport.Scale));
        }

        internal string FindNodeKey(VisualElement pressTarget)
        {
            // silent-ok: ノードの外の押下は Web と同じく何も選ばない / 押下の対象にノードが無いだけで異常ではない
            return _scene.TryFindNodeKey(pressTarget, out var key) ? key : null;
        }

        internal void TapNode(string pressedKey)
        {
            // silent-ok: ノードの外の押下は何も選ばない / FindNodeKey が null を返す正常系
            if (pressedKey == null) return;
            if (!_scene.ContainsKey(pressedKey))
            {
                // 押している間に SetGraph でそのキーが無くなった。無いノードへは流さず知らせる
                // The key vanished through SetGraph during the press; do not tap a missing node and report it
                Debug.LogWarning($"[NodeGraphView] tap dropped: node '{pressedKey}' is no longer in the graph");
                return;
            }
            _onNodeTapped.OnNext(pressedKey);
        }

        private void OnWheel(WheelEvent evt)
        {
            // 祖先のスクロールへ渡さず、滑走を止める（Web の preventDefault と inertia.cancel）
            // Keep it from ancestor scrolling and stop the glide (the Web preventDefault and inertia.cancel)
            evt.StopPropagation();
            _pan.CancelGlide();

            // カーソルを内容 box の座標へ直し、1 ノッチを Web の deltaY（WebWheelScale。A-71・A-118）へ換算してズームする（横成分は無視）
            // Convert the cursor to content-box coordinates, scale one notch to the Web deltaY (WebWheelScale; A-71, A-118), and zoom (horizontal ignored)
            var cursor = this.WorldToLocal(evt.mousePosition) - contentRect.position;
            var webDeltaY = evt.delta.y * WebWheelScale.PixelsPerNotch / WheelEvent.scrollDeltaPerTick;
            SetViewport(NodeGraphViewportMath.ZoomAt(_viewport, cursor, webDeltaY));
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // 隠す前に視点が動いていなければ、寸法の変化で中央に見えていた点を保つ（0 からの初回は動かさない。Web useKeepViewCenterOnResize）
            // Only when not restored, keep the centered point across size changes (not from zero; the Web useKeepViewCenterOnResize)
            var size = contentRect.size;
            // 0 へ縮む回（display: none の祖先）は PointerLeave が来ないので手のカーソルを放し、追従も記録もしない。視点が動いていれば保持し、開き直しても寸法に追従しない（Web の復元は寸法に追従しない）
            // A shrink to 0 (display: none ancestor) sends no PointerLeave, so release the hand cursor and neither follow nor record; a moved view is held and does not follow the size on reopen (the Web restore never follows)
            if (size.x <= 0f || size.y <= 0f)
            {
                _pointerFeedback.ReleaseAll();
                _viewportHeld |= _hasEmittedViewport;
                return;
            }
            var keepsCenter = !_viewportHeld && _hasClipInset && 0f < _lastContentSize.x && 0f < _lastContentSize.y && size != _lastContentSize;
            if (keepsCenter) SetViewport(NodeGraphViewportMath.ShiftForResize(_viewport, _lastContentSize, size));
            if (_hasClipInset) _lastContentSize = size;
            CenterOnInitialFocusOnce();
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // silent-ok: 読めなければ前回の逃げのまま / UssCustomStyleReader が LogError を出す
            if (!UssCustomStyleReader.TryReadPixels(customStyle, ClipInsetProperty, out var inset)) return;
            // silent-ok: 同じ値なら書き直さず、style の再解決の繰り返しを起こさない / 正常系
            if (_hasClipInset && Mathf.Approximately(inset, _clipInset)) return;

            // クリップの逃げを padding と同量の負の margin で当てる（Web の calc(-1 * inset)。A-47）
            // Apply the clip clearance as padding plus an equal negative margin (the Web calc(-1 * inset); A-47)
            _clipInset = inset;
            _hasClipInset = true;
            style.paddingLeft = inset;
            style.paddingRight = inset;
            style.paddingTop = inset;
            style.paddingBottom = inset;
            style.marginLeft = -inset;
            style.marginRight = -inset;
            style.marginTop = -inset;
            style.marginBottom = -inset;
        }

        private void CenterOnInitialFocusOnce()
        {
            // silent-ok: 寄せた後・保持した後・ノードか注目点か逃げか寸法が未着の間は寄せない / 揃った次の SetGraph か寸法の変化で 1 回だけ寄る（Web と同じ）
            if (_hasCentered || _viewportHeld || !_scene.HasNodes || !_initialFocus.TryGetPoint(out var focus) || !_hasClipInset || contentRect.width <= 0f) return;

            // 事前のズームの倍率を保ったまま、注目点を内容 box の中央に据える
            // Center the focus point in the content box while keeping any earlier zoom scale
            _hasCentered = true;
            SetViewport(NodeGraphViewportMath.CenterOn(_scene.ToCanvasPoint(focus), contentRect.size, _viewport.Scale));
        }

        private void SetViewport(NodeGraphViewport viewport)
        {
            _viewport = viewport;
            _hasEmittedViewport = true;
            ApplyViewport();
        }

        private void ApplyViewport()
        {
            // CSS の translate(x, y) scale(s)（transform-origin: 0 0）と同じ変換をキャンバスへ当てる
            // Apply the same transform as CSS translate(x, y) scale(s) with transform-origin 0 0
            _canvas.style.translate = new Translate(_viewport.X, _viewport.Y);
            _canvas.style.scale = new Scale(new Vector2(_viewport.Scale, _viewport.Scale));
        }
    }
}
