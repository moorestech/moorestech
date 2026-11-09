using System;
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
    /// ビューポートの保持は画面が持つ。変化は OnViewportChanged で流し、作り直した直後に RestoreViewport で戻すと初回の中央寄せと寸法の追従をしない
    /// The screen keeps the viewport; changes flow out on OnViewportChanged, and RestoreViewport right after rebuilding skips initial centering and resize-follow
    /// </remarks>
    public sealed class NodeGraphView : VisualElement
    {
        // Web のホイール 1 ノッチの deltaY（CefUnity の ScrollInputPipeline.WheelPixelsPerStep。GameScrollView と同じ値。A-118）
        // The Web deltaY per wheel notch (CefUnity ScrollInputPipeline.WheelPixelsPerStep; same as GameScrollView; A-118)
        private const float WebWheelDeltaPerNotch = 60f;
        private const string ClipInsetProperty = "--node-graph-clip-inset";

        private readonly VisualElement _canvas;
        private readonly NodeGraphScene _scene;
        private readonly NodeGraphPanController _pan;
        private readonly Subject<string> _onNodeTapped = new();
        private readonly Subject<NodeGraphViewport> _onViewportChanged = new();
        private bool _restored;
        private NodeGraphViewport _viewport;
        private NodeGraphFocus _initialFocus = NodeGraphFocus.None;
        private bool _hasCentered;
        private bool _hasClipInset;
        private float _clipInset;
        private Vector2 _lastContentSize;

        public IObservable<string> OnNodeTapped => _onNodeTapped;
        public IObservable<NodeGraphViewport> OnViewportChanged => _onViewportChanged;

        public NodeGraphView(string testIdPrefix)
        {
            // 根とキャンバスと線・ノードの場面を作る
            // Build the root, the canvas and the edge/node scene
            name = $"{testIdPrefix}-viewport";
            AddToClassList("node-graph");
            _canvas = new VisualElement { name = $"{testIdPrefix}-canvas" };
            _canvas.AddToClassList("node-graph__canvas");
            Add(_canvas);
            _scene = new NodeGraphScene(_canvas);
            _viewport = NodeGraphViewport.Identity;
            ApplyViewport();

            // 押下とパンは Foundation の UiPressGesture、ホイール・寸法・つまみは自分で受ける
            // Press and pan go through Foundation UiPressGesture; wheel, size and knobs are handled here
            _pan = new NodeGraphPanController(this);
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

        public void RestoreViewport(NodeGraphViewport viewport)
        {
            // 画面が保持した値へ戻し、以後の初回の中央寄せと寸法の追従を止める。戻した値は流さない（Web のマウント時の復元と同じ）
            // Return to the screen-held value and stop initial centering and resize-follow; the restored value is not emitted (same as the Web mount restore)
            _restored = true;
            _viewport = viewport;
            ApplyViewport();
        }

        internal void PanBy(Vector2 delta)
        {
            SetViewport(new NodeGraphViewport(_viewport.X + delta.x, _viewport.Y + delta.y, _viewport.Scale));
        }

        internal void TapAt(VisualElement pressTarget)
        {
            // silent-ok: ノードの外の押下は Web と同じく何も選ばない / 押下の対象にノードが無いだけで異常ではない
            if (!_scene.TryFindNodeKey(pressTarget, out var key)) return;
            _onNodeTapped.OnNext(key);
        }

        private void OnWheel(WheelEvent evt)
        {
            // 祖先のスクロールへ渡さず、滑走を止める（Web の preventDefault と inertia.cancel）
            // Keep it from ancestor scrolling and stop the glide (the Web preventDefault and inertia.cancel)
            evt.StopPropagation();
            _pan.CancelGlide();

            // カーソルを内容 box の座標へ直し、1 ノッチを Web の deltaY 60 へ換算してズームする（横成分は無視）
            // Convert the cursor to content-box coordinates, scale one notch to the Web deltaY 60, and zoom (horizontal ignored)
            var cursor = this.WorldToLocal(evt.mousePosition) - contentRect.position;
            var webDeltaY = evt.delta.y * WebWheelDeltaPerNotch / WheelEvent.scrollDeltaPerTick;
            SetViewport(NodeGraphViewportMath.ZoomAt(_viewport, cursor, webDeltaY));
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // 復元しなかったときだけ、寸法の変化で中央に見えていた点を保つ（0 からの初回は動かさない。Web useKeepViewCenterOnResize）
            // Only when not restored, keep the centered point across size changes (not from zero; the Web useKeepViewCenterOnResize)
            var size = contentRect.size;
            // silent-ok: 0 へ縮む回（display: none の祖先）は追従も記録もしない / 開き直した寸法で次の回から追従する
            if (size.x <= 0f || size.y <= 0f) return;
            var keepsCenter = !_restored && _hasClipInset && _lastContentSize.x > 0f && _lastContentSize.y > 0f && size != _lastContentSize;
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
            // silent-ok: 寄せた後・復元した後・ノードか注目点か逃げか寸法が未着の間は寄せない / 揃った次の SetGraph か寸法の変化で 1 回だけ寄る（Web と同じ）
            if (_hasCentered || _restored || !_scene.HasNodes || !_initialFocus.TryGetPoint(out var focus) || !_hasClipInset || contentRect.width <= 0f) return;

            // 事前のズームの倍率を保ったまま、注目点を内容 box の中央に据える
            // Center the focus point in the content box while keeping any earlier zoom scale
            _hasCentered = true;
            SetViewport(NodeGraphViewportMath.CenterOn(_scene.ToCanvasPoint(focus), contentRect.size, _viewport.Scale));
        }

        private void SetViewport(NodeGraphViewport viewport)
        {
            _viewport = viewport;
            ApplyViewport();
            _onViewportChanged.OnNext(viewport);
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
