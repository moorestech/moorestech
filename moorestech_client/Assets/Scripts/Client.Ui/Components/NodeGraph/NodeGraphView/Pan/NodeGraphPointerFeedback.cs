using System;
using Client.Ui.Foundation.Input.OsCursor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ポインタへの見た目の手応え。ビューポートの上では地の層で掴む手、パン中はドラッグの層で握った手を OS カーソルへ要求し、パン中のクラスも付け外す。カードの指は画面側が UiOsCursorHoverRequest で要素の層に要求し、地の掴む手に勝つ。web: TreeView.module.css の .viewport { cursor: grab } と :active の grabbing
    /// Pointer feedback: requests the grab hand on the surface layer over the viewport and the grabbing hand on the drag layer while panning, and toggles the panning class; screens request the card pointer on the element layer via UiOsCursorHoverRequest, which beats the surface grab; web: TreeView.module.css .viewport cursor grab and :active grabbing
    /// </summary>
    internal sealed class NodeGraphPointerFeedback
    {
        private const string PanningClass = "node-graph--panning";

        private readonly VisualElement _viewport;
        private readonly IUiOsCursor _osCursor;
        private readonly UiOsCursorHoverRequest _hover;
        private IDisposable _panRequest;

        public NodeGraphPointerFeedback(VisualElement viewport, IUiOsCursor osCursor)
        {
            _viewport = viewport;
            _osCursor = osCursor;

            // 掴む手の出入り・取り外し・大きさ 0 での解放は Foundation の部品に任せ、ここはパン中の握った手だけ持つ
            // The Foundation part handles the grab hand's enter, leave, detach and zero size; this owns only the grabbing hand while panning
            _hover = new UiOsCursorHoverRequest(viewport, osCursor, UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
            viewport.RegisterCallback<DetachFromPanelEvent>(_ => StopPanning());
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
            if (_viewport.worldBound.Contains(panelPosition)) _hover.Hold();
            else _hover.Release();
            StopPanning();
        }

        public void AbortPanning()
        {
            // silent-ok: パン前の取消は形もクラスも変えていない / 正常系
            if (_panRequest == null) return;
            StopPanning();
        }

        // 大きさ 0 で隠れた回はパン中の握った手も放して既定へ戻す（掴む手は部品が自分で外す）
        // On a zero-size hide also drop the grabbing hand and return to the default (the part withdraws the grab hand itself)
        internal void ReleaseAll()
        {
            _hover.Release();
            StopPanning();
        }

        private void StopPanning()
        {
            _viewport.RemoveFromClassList(PanningClass);
            _panRequest?.Dispose();
            _panRequest = null;
        }
    }
}
