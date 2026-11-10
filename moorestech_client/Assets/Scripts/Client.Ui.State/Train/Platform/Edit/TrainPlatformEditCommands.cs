using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.Train.Platform.Edit
{
    /// <summary>
    /// 駅名と移送モードを送る。結果はトーストへ渡し、駅名の拒否は送った回の下書きだけを戻す
    /// Sends station names and transfer modes; results go to the toaster and a station name refusal reverts only the draft that sent
    /// </summary>
    public sealed class TrainPlatformEditCommands
    {
        private const string SetNameCommandName = "train_station.set_name";
        private const string SetModeCommandName = "train_platform.set_transfer_mode";
        private readonly TrainStationNameEditState _state;
        private readonly ITrainBlockCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public TrainPlatformEditCommands(TrainStationNameEditState state, ITrainBlockCommands commands, UiCommandResultToaster toaster)
        {
            _state = state;
            _commands = commands;
            _toaster = toaster;
        }

        public void EditStationName(string input)
        {
            _state.Edit(input);
        }

        // 戻り値は無い。サーバーの結果は届いてからトーストへ渡す
        // No return value; the server result is reported to the toaster when it arrives
        public void ApplyStationName()
        {
            ApplyAsync(_state.Draft, _state.Draft.Input).Forget();
        }

        // 選択は替えず目標のモードを送るだけ。結果は公開で反映され、拒否だけトーストへ
        // The selection is not changed; only the target mode is sent and the publication reflects it, with refusals toasted
        public void SelectTransferMode(TrainPlatformTransferMode mode)
        {
            SendModeAsync(mode).Forget();
        }

        private async UniTaskVoid ApplyAsync(TrainStationNameDraft draftAtSend, string name)
        {
            var result = await _commands.SetStationName(name);
            _toaster.Report(SetNameCommandName, result);
            if (result is UiCommandRejected) _state.RevertIfCurrent(draftAtSend);
        }

        private async UniTaskVoid SendModeAsync(TrainPlatformTransferMode mode)
        {
            var result = await _commands.SetTransferMode(mode);
            _toaster.Report(SetModeCommandName, result);
        }
    }
}
