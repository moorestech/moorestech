using System;
using Client.Ui.Foundation.Input.OsCursor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ポインタへの見た目の手応え。ビューポートの上では地の層で掴む手、パン中はドラッグの層で握った手を OS カーソルへ要求し、パン中のクラスも付け外す。カードの指は画面側が要素の層で要求し、地の掴む手に勝つ。web: TreeView.module.css の .viewport { cursor: grab } と :active の grabbing
    /// Pointer feedback: requests the grab hand on the surface layer over the viewport and the grabbing hand on the drag layer while panning, and toggles the panning class; screens request the card pointer on the element layer, which beats the surface grab; web: TreeView.module.css .viewport cursor grab and :active grabbing
    /// </summary>
    internal sealed class NodeGraphPointerFeedback
    {
        private const string PanningClass = "node-graph--panning";

        private readonly VisualElement _viewport;
        private readonly IUiOsCursor _osCursor;
        private IDisposable _hoverRequest;
        private IDisposable _panRequest;

        public NodeGraphPointerFeedback(VisualElement viewport, IUiOsCursor osCursor)
        {
            _viewport = viewport;
            _osCursor = osCursor;

            // 出入りはビューポート自身の分だけ見る（子の出入りは中に居るまま）
            // Only the viewport's own enter and leave count (moving among children stays inside)
            viewport.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            viewport.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            viewport.RegisterCallback<DetachFromPanelEvent>(_ => ReleaseAll());
        }

        public void StartPanning()
        {
            // silent-ok: 握った手は取得済み（ドラッグ中は毎回呼ばれる） / 正常系
            if (_panRequest != null) return;
            _viewport.AddToClassList(PanningClass);
            _panRequest = _osCursor.Request(UiOsCursorShape.Grabbing, UiOsCursorLayer.Drag);
        }

        public void EndPanning(Vector2 panelPosition)
        {
            // 離した位置がビューポートの上なら掴む手を残し、外なら外す。先に掴む手を決めてから握った手を外す
            // Keep the grab hand when released over the viewport, drop it outside; settle the grab hand before dropping the grabbing one
            if (_viewport.worldBound.Contains(panelPosition)) RequestHover();
            else ReleaseHover();
            StopPanning();
        }

        public void AbortPanning()
        {
            // silent-ok: パン前の取消は形もクラスも変えていない / 正常系
            if (_panRequest == null) return;
            StopPanning();
        }

        // 非表示や取り外しで PointerLeave が来ない経路のため、要求を全部放して既定へ戻す
        // Releases every request and returns to the default for paths without a PointerLeave, such as hiding or detaching
        internal void ReleaseAll()
        {
            ReleaseHover();
            StopPanning();
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            // silent-ok: 子の出入りの事象は無視する / 中に居る間の正常系
            if (evt.target != _viewport) return;
            RequestHover();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            // silent-ok: 子の出入りの事象は無視する / 中に居る間の正常系
            if (evt.target != _viewport) return;
            // パン中（捕捉中）は握った手がドラッグの層にあるので、外へ出ても握った手のまま
            // While panning (captured) the grabbing hand sits on the drag layer, so it stays grabbing outside
            ReleaseHover();
        }

        private void RequestHover()
        {
            _hoverRequest ??= _osCursor.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
        }

        private void ReleaseHover()
        {
            _hoverRequest?.Dispose();
            _hoverRequest = null;
        }

        private void StopPanning()
        {
            _viewport.RemoveFromClassList(PanningClass);
            _panRequest?.Dispose();
            _panRequest = null;
        }
    }
}
