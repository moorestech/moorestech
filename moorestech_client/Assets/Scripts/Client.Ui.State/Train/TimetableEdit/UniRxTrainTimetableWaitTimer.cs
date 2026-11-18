using System;
using UniRx;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 実時間の待ち。UI の通信待ちでゲームロジックの経過ではないので実時間でよい（Web の通信の時間切れと同じ）
    /// Real-time wait; this is a UI transport wait, not game logic time (same as the Web transport timeout)
    /// </summary>
    public sealed class UniRxTrainTimetableWaitTimer : ITrainTimetableWaitTimer
    {
        public IObservable<Unit> After(TimeSpan delay)
        {
            return Observable.Timer(delay, Scheduler.MainThreadIgnoreTimeScale).AsUnitObservable();
        }
    }
}
