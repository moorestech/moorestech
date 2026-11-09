using Client.Ui.Foundation.Input.Gesture;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.DragDrop
{
    /// <summary>
    /// 源の押下をタップ・ドロップへ分類する
    /// Classifies source presses into tap or drop
    /// </summary>
    internal sealed class UiDragSourceGesture<TTarget> : IUiPressGestureHandler where TTarget : class
    {
        private readonly VisualElement _source;
        private IUiDragPayload<TTarget> _payload;

        public UiDragSourceGesture(VisualElement source, IUiDragPayload<TTarget> payload)
        {
            _source = source;
            _payload = payload;
        }

        public void SetPayload(IUiDragPayload<TTarget> payload)
        {
            _payload = payload;
        }

        public void OnPressStarted(Vector2 panelPosition)
        {
            // Web の preventDefault（文字の選択・フォーカス移動の抑止）にあたる既定の動作は UITK に無い
            // UITK has no default action matching the Web preventDefault (text selection, focus shift)
        }

        public void OnDragMoved(UiPressMove move)
        {
            // 掴み絵は持たない（Web のホットバーのドラッグに絵が無い。掴み絵の層は C4-e）
            // No drag image (the Web hotbar drag has none; the grab layer is C4-e)
        }

        public void OnDragEnded(Vector2 panelPosition)
        {
            // 最寄りの同型の落とし先を引く。無ければ枠外
            // Find the nearest target of this type from the element under the release point, else outside (Web elementFromPoint -> closest)
            var picked = _source.panel.Pick(panelPosition);
            if (picked != null && UiDragDrop.TryFindTarget<TTarget>(picked, out var target))
            {
                _payload.OnDropped(target);
                return;
            }
            _payload.OnDroppedOutside();
        }

        public void OnTapped(VisualElement pressTarget)
        {
            _payload.OnTapped();
        }

        public void OnAborted(bool dragged)
        {
            // silent-ok: 中断（取消・捕捉の喪失・パネルから外れ）はタップにもドロップにもしない Web の契約 / UiPressGestureTest「捕捉を失うと中断で…」が分類を固定する
        }
    }
}
