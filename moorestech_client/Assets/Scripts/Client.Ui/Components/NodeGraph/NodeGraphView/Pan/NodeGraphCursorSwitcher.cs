using Client.Ui.Foundation.Input.OsCursor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.NodeGraph
{
    /// <summary>
    /// ビューポートの上では掴む手、パン中は握った手を OS カーソルへ当てる。web: TreeView.module.css の .viewport { cursor: grab } と :active の grabbing
    /// Applies the grab hand over the viewport and the grabbing hand while panning to the OS cursor; web: TreeView.module.css .viewport cursor grab and :active grabbing
    /// </summary>
    internal sealed class NodeGraphCursorSwitcher
    {
        private readonly VisualElement _viewport;
        private readonly IUiOsCursor _osCursor;
        private UiOsCursorShape _applied = UiOsCursorShape.Default;
        private bool _hovering;
        private bool _panning;

        public NodeGraphCursorSwitcher(VisualElement viewport, IUiOsCursor osCursor)
        {
            _viewport = viewport;
            _osCursor = osCursor;

            // 出入りはビューポート自身の分だけ見る（子の出入りは中に居るまま）
            // Only the viewport's own enter and leave count (moving among children stays inside)
            viewport.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            viewport.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            viewport.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void StartPanning()
        {
            _panning = true;
            Apply(UiOsCursorShape.Grabbing);
        }

        public void EndPanning(Vector2 panelPosition)
        {
            // 離した位置がビューポートの上なら掴む手、外なら既定へ戻す
            // Return to the grab hand when released over the viewport, otherwise to the default
            _panning = false;
            _hovering = _viewport.worldBound.Contains(panelPosition);
            Apply(_hovering ? UiOsCursorShape.Grab : UiOsCursorShape.Default);
        }

        public void AbortPanning()
        {
            // silent-ok: パン前の取消は形を変えていない / 正常系
            if (!_panning) return;
            _panning = false;
            Apply(_hovering ? UiOsCursorShape.Grab : UiOsCursorShape.Default);
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            // silent-ok: 子の出入りの事象は無視する / 中に居る間の正常系
            if (evt.target != _viewport) return;
            _hovering = true;
            if (!_panning) Apply(UiOsCursorShape.Grab);
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            // silent-ok: 子の出入りの事象は無視する / 中に居る間の正常系
            if (evt.target != _viewport) return;
            // パン中（捕捉中）は外へ出ても握った手のまま。離した時に決める
            // While panning (captured) keep the grabbing hand outside; release decides
            _hovering = false;
            if (!_panning) Apply(UiOsCursorShape.Default);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            // 外された部品が手の形を残さないよう既定へ戻す
            // Return to the default so a detached view never leaves a hand shape behind
            _hovering = false;
            _panning = false;
            Apply(UiOsCursorShape.Default);
        }

        private void Apply(UiOsCursorShape shape)
        {
            // silent-ok: 同じ形は当て直さない（他の部品が当てた既定を上書きしないため） / 正常系
            if (shape == _applied) return;
            _applied = shape;
            _osCursor.SetShape(shape);
        }
    }
}
