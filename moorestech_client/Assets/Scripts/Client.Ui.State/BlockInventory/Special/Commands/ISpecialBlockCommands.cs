using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 特殊族の操作（Web の filter_splitter.*・electric_to_gear.set_output_mode・space_elevator.connect・rocket_launch_pad.command）。表示は楽観更新せずサーバーの後続の状態で変わる
    /// Special-family commands (the Web filter_splitter.*, electric_to_gear.set_output_mode, space_elevator.connect, rocket_launch_pad.command); the display is not updated optimistically
    /// </summary>
    public interface ISpecialBlockCommands
    {
        UniTask<UiCommandResult> SetFilterMode(int directionIndex, FilterSplitterMode mode);
        UniTask<UiCommandResult> SetFilterItem(int directionIndex, int slotIndex, bool clear);
        UniTask<UiCommandResult> SetElectricToGearMode(int modeIndex);
        UniTask<UiCommandResult> ConnectSpaceElevator();
        UniTask<UiCommandResult> SendRocketCommand(RocketCommand command);
    }
}
