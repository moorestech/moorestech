using UniRx;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 開いている駅・プラットフォームの描画値の口
    /// Source of the open station or platform's draw values
    /// </summary>
    public interface ITrainPlatformBodySource
    {
        IReadOnlyReactiveProperty<TrainPlatformBodyState> Current { get; }
    }
}
