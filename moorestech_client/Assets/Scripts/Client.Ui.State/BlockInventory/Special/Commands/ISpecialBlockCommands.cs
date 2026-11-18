using Client.Ui.State.BlockInventory.Logic.Special;

namespace Client.Ui.State.BlockInventory.Special.Commands
{
    /// <summary>
    /// 特殊族の操作。表示は楽観更新せず、結果は実装がトーストへ渡す
    /// Special-family commands; the display is not updated optimistically and the implementation hands results to the toaster
    /// </summary>
    public interface ISpecialBlockCommands
    {
        void SetFilterMode(int directionIndex, FilterSplitterMode mode);
        void SetFilterItem(int directionIndex, int slotIndex, bool clear);
        void SetElectricToGearMode(int modeIndex);
        void ConnectSpaceElevator();
        void SendRocketCommand(RocketCommand command);
    }
}
