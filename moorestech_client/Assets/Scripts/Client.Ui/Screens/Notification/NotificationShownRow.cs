using System;
using Client.Ui.Screens.Notification.Parts;

namespace Client.Ui.Screens.Notification
{
    /// <summary>
    /// ホストが描いている 1 行の要素と、いま回している生存尺の持ち物
    /// Holds a rendered row element and the lifetime currently running for it
    /// </summary>
    internal sealed class NotificationShownRow
    {
        public readonly NotificationRowView View;
        private NotificationRowLifetimeHandle _motion;
        private IDisposable _exitSubscription;
        public int LifetimeEpoch { get; private set; }

        public NotificationShownRow(NotificationRowView view)
        {
            View = view;
        }

        public void SetLifetime(int lifetimeEpoch, NotificationRowLifetimeHandle motion, IDisposable exitSubscription)
        {
            LifetimeEpoch = lifetimeEpoch;
            _motion = motion;
            _exitSubscription = exitSubscription;
        }

        // 外れている間に始めた行は動きを持たない。取り消した生存の退場の購読も切り、古い終わりが行を消す余地を残さない
        // Also drop the cancelled lifetime's exit subscription so a stale end can never remove the row
        public void StopLifetime()
        {
            _motion?.Cancel();
            _exitSubscription?.Dispose();
        }
    }
}
