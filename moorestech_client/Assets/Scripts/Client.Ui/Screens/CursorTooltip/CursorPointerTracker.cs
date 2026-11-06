using System;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CursorTooltip
{
    /// <summary>
    /// 実画面のポインタ移動をパネル座標で受けて View へ渡す。ツールチップが隠れている間も、何も拾わない所でも受ける（Web の window の pointermove）
    /// Hands real pointer moves to the view in panel coordinates, also while hidden and where nothing is pickable (Web window pointermove)
    /// </summary>
    internal sealed class CursorPointerTracker
    {
        private readonly IDisposable _subscription;

        public CursorPointerTracker(IPanel panel, ICursorTooltipScreen screen, CursorTooltipView view)
        {
            // パネルの PointerMoveEvent は何も拾わない所では届かない（実測 received=none）ので、Client.Input の購読口から受ける
            // Panel PointerMoveEvent does not arrive where nothing is pickable (measured received=none), so receive from the Client.Input stream
            _subscription = screen.OnPointerMovedInPanel(panel).Subscribe(view.SetPointer);
        }

        public void Detach()
        {
            _subscription.Dispose();
        }
    }
}
