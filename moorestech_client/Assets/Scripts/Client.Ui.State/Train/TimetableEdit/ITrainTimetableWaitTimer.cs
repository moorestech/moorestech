using System;
using UniRx;

namespace Client.Ui.State.Train.TimetableEdit
{
    /// <summary>
    /// 受理後の公開待ちの時間切れ。テストで手で起こせるよう口にする
    /// Timeout for awaiting publication after acceptance; an interface so tests can fire it by hand
    /// </summary>
    public interface ITrainTimetableWaitTimer
    {
        IObservable<Unit> After(TimeSpan delay);
    }
}
