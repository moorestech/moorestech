using Client.Ui.State.Train.Timetable;
using UniRx;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 源の偽物。値はテストが Set で押し込む
    /// Fake sources; tests push values with Set
    /// </summary>
    public sealed class FakeTrainSources : ITrainTimetableSource
    {
        private readonly ReactiveProperty<TrainTimetableViewState> _timetable = new(TrainTimetableViewState.Loading);

        IReadOnlyReactiveProperty<TrainTimetableViewState> ITrainTimetableSource.Current => _timetable;

        public void SetTimetable(TrainTimetableViewState state)
        {
            _timetable.Value = state;
        }
    }
}
