using UniRx;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の下書きの器から入力欄の描画値を配る
    /// Publishes the station name field's draw value from the draft holder
    /// </summary>
    public sealed class TrainPlatformEditModel
    {
        private readonly TrainStationNameEditState _state;

        public IReadOnlyReactiveProperty<string> StationNameInput => _state.Input;

        public TrainPlatformEditModel(TrainStationNameEditState state)
        {
            _state = state;
        }
    }
}
