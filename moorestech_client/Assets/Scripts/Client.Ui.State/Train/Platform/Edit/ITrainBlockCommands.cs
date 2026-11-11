using Client.Ui.State.Commands;
using Client.Ui.State.Train.Platform;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 開いている駅・プラットフォームへの操作。対象はその時点で開いているブロック（Web と同じ）
    /// Operations on the open station or platform; the target is the block open at that moment (same as Web)
    /// </summary>
    public interface ITrainBlockCommands
    {
        UniTask<UiCommandResult> SetStationName(string name);
        UniTask<UiCommandResult> SetTransferMode(TrainPlatformTransferMode mode);
    }
}
