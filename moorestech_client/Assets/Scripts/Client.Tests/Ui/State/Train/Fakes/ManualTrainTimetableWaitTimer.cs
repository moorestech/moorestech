using System;
using System.Collections.Generic;
using Client.Ui.State.Train.TimetableEdit;
using UniRx;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 時間切れをテストが手で起こす
    /// Lets tests fire the timeout by hand
    /// </summary>
    public sealed class ManualTrainTimetableWaitTimer : ITrainTimetableWaitTimer
    {
        public List<TimeSpan> RequestedDelays { get; } = new();
        private readonly Subject<Unit> _elapsed = new();

        public IObservable<Unit> After(TimeSpan delay)
        {
            RequestedDelays.Add(delay);
            return _elapsed.Take(1);
        }

        public void Elapse()
        {
            _elapsed.OnNext(Unit.Default);
        }
    }
}
