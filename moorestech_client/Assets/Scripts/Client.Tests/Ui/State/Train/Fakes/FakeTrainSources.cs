using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Timetable;
using UniRx;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 3 つの源の偽物。値はテストが Set で押し込む
    /// Fakes of the three sources; tests push values with Set
    /// </summary>
    public sealed class FakeTrainSources : ITrainTimetableSource, ITrainInventoryBodySource, ITrainPlatformBodySource
    {
        private readonly ReactiveProperty<TrainTimetableViewState> _timetable = new(TrainTimetableViewState.Loading);
        private readonly ReactiveProperty<TrainInventoryBodyViewModel> _body = new(TrainInventoryBodyViewModel.Closed);
        private readonly ReactiveProperty<TrainPlatformBodyState> _platform = new(TrainPlatformBodyState.Closed);

        IReadOnlyReactiveProperty<TrainTimetableViewState> ITrainTimetableSource.Current => _timetable;
        IReadOnlyReactiveProperty<TrainInventoryBodyViewModel> ITrainInventoryBodySource.Current => _body;
        IReadOnlyReactiveProperty<TrainPlatformBodyState> ITrainPlatformBodySource.Current => _platform;

        public void SetTimetable(TrainTimetableViewState state)
        {
            _timetable.Value = state;
        }

        public void SetBody(TrainInventoryBodyViewModel body)
        {
            _body.Value = body;
        }

        public void SetPlatform(TrainPlatformBodyState platform)
        {
            _platform.Value = platform;
        }
    }
}
