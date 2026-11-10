using UniRx;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名の入力欄の描画値。Presenter が購読する
    /// Draw value of the station name field; the Presenter subscribes to it
    /// </summary>
    public interface ITrainPlatformEditModel
    {
        IReadOnlyReactiveProperty<string> StationNameInput { get; }
    }
}
