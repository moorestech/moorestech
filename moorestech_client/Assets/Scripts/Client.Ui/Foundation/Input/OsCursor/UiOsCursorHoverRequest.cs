using System;
using Client.Ui.Foundation.Input.Pointer;
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
        private readonly IUiScreenPointer _pointer;
        private readonly UiOsCursorShape _shape;
        private readonly UiOsCursorLayer _layer;
        private IDisposable _request;
        private bool _isSuspended;

        public UiOsCursorHoverRequest(VisualElement element, IUiOsCursor cursor, IUiScreenPointer pointer, UiOsCursorShape shape, UiOsCursorLayer layer)
        {
            _element = element;
            _cursor = cursor;
            _pointer = pointer;
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

        // 押せるかを指の要求へ揃える。押せない間は乗っても指を出さない
        // Align the hand request with pressability; while unpressable, hovering never shows the hand
        public void SetPressable(bool pressable)
        {
            if (pressable) Resume();
            else Suspend();

            #region Internal

            // 押せなくしたときに呼ぶ。要求を外す
            // Call when unpressable: withdraw the request
            void Suspend()
            {
                _isSuspended = true;
                Release();
            }

            // 押せる戻しで呼ぶ。拾わない間の移動は事象で届かないので、その時点の実位置が要素の上なら指を出し直す
            // Call when pressable again: moves while unpickable send no event, so re-request the hand when the actual position is over the element now
            void Resume()
            {
                // silent-ok: 押せるままの重ね呼びは何もしない / 正常系
                if (!_isSuspended) return;
                _isSuspended = false;

                // silent-ok: パネルから外れた間の再開は位置を測れず、載せ直しの Enter が出し直す / 外れた要素は指を出さないのが正常
                var panel = _element.panel;
                if (panel == null) return;
                var onPanel = _pointer.ScreenToPanel(panel, _pointer.CurrentPointerOnScreen());
                // silent-ok: 要素の外なら指を出さないのが正常 / 次の Enter で取る
                if (!_element.worldBound.Contains(onPanel)) return;
                // 覆われていれば Hold せず UITK の Enter を待つ（覆いの下の要素へ指を取らない）
                // When covered, do not Hold and wait for UITK's Enter (never take the hand for an element underneath a cover)
                var picked = panel.Pick(onPanel);
                if (picked != null && (picked == _element || _element.Contains(picked))) Hold();
            }

            #endregion
        }

        private void OnPointerEnter(PointerEnterEvent evt)
        {
            // silent-ok: 子の出入りの事象は無視する / 中に居る間の正常系
            if (evt.target != _element) return;
            // silent-ok: 押せない間（無効で拾うままの部品）は指を出さないのが正常 / 押せる戻しの Resume が出し直す
            if (_isSuspended) return;
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
