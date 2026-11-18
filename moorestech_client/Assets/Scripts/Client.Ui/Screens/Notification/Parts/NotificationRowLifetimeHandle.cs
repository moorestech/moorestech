using System;
using Client.Ui.Foundation.Motion.Playback;
using UniRx;

namespace Client.Ui.Screens.Notification.Parts
{
    /// <summary>
    /// 行の生存の 2 本をまとめて取り消し、退場の終わりを知らせる
    /// Cancels both tweens of a row's lifetime together and reports the exit's end
    /// </summary>
    public sealed class NotificationRowLifetimeHandle
    {
        private readonly UiMotionHandle _enter;
        private readonly UiMotionHandle _exit;

        public IObservable<Unit> ExitCompleted => _exit.Completed;

        public NotificationRowLifetimeHandle(UiMotionHandle enter, UiMotionHandle exit)
        {
            _enter = enter;
            _exit = exit;
        }

        public void Cancel()
        {
            _enter.Cancel();
            _exit.Cancel();
        }
    }
}
