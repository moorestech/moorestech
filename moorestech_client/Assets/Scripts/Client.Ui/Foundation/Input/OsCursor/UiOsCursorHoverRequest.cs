using System;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// 要素に乗っている間だけ OS カーソルの形を要求する。PointerLeave・取り外し（DetachFromPanel）・大きさ 0（display:none の祖先等で PointerLeave が来ない）で必ず外す。要求の外し損ねは層つきのスタックで下の層を負かし続けるので、要素に紐づく要求はこれを使う
    /// Requests an OS cursor shape only while the pointer is over the element; always withdraws on PointerLeave, DetachFromPanel and zero size (a display:none ancestor and the like send no PointerLeave). A leaked request keeps beating lower layers in the layered stack, so element-bound requests use this
    /// </summary>
    public sealed class UiOsCursorHoverRequest
    {
        private readonly VisualElement _element;
        private readonly IUiOsCursor _cursor;
        private readonly UiOsCursorShape _shape;
        private readonly UiOsCursorLayer _layer;
        private IDisposable _request;

        public UiOsCursorHoverRequest(VisualElement element, IUiOsCursor cursor, UiOsCursorShape shape, UiOsCursorLayer layer)
        {
            _element = element;
            _cursor = cursor;
            _shape = shape;
            _layer = layer;

            // 出入りは要素自身の分だけ見る（子の出入りは中に居るまま）
            // Only the element's own enter and leave count (moving among children stays inside)
            element.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            element.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            element.RegisterCallback<DetachFromPanelEvent>(_ => Release());
            element.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        // ポインタが乗っている前提で要求する（離した位置が要素の上だった等、Enter を待たずに取り直す経路用）。取得済みなら何もしない
        // Requests assuming the pointer is over the element (for re-taking without an Enter, e.g. released over it); no-op when already held
        public void Hold()
        {
            _request ??= _cursor.Request(_shape, _layer);
        }

        public void Release()
        {
            _request?.Dispose();
            _request = null;
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            // silent-ok: 子の出入りの事象は無視する / 中に居る間の正常系
            if (evt.target != _element) return;
            Hold();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            // silent-ok: 子の出入りの事象は無視する / 中に居る間の正常系
            if (evt.target != _element) return;
            Release();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // 0 へ縮む回（display:none の祖先等）は PointerLeave が来ないのでここで外す
            // A shrink to 0 (a display:none ancestor and the like) sends no PointerLeave, so withdraw here
            if (evt.newRect.width <= 0f || evt.newRect.height <= 0f) Release();
        }
    }
}
