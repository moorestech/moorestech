using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 特殊族の操作。表示は楽観更新しない
    /// Special-family commands; the display is not updated optimistically
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
