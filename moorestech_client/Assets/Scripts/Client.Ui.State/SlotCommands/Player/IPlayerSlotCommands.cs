using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// 持ち物・装備・ホットバーの枠の操作の口。結果は呼び手が UiCommandResultToaster.Report(PlayerSlotCommands.CommandName, result) へ渡す
    /// Port for inventory, equipment and hotbar slot gestures; callers pass the result to UiCommandResultToaster.Report(PlayerSlotCommands.CommandName, result)
    /// </summary>
    public interface IPlayerSlotCommands
    {
        UiCommandResult LeftDown(PlayerSlotRef slot, bool shift);
        UiCommandResult RightDown(PlayerSlotRef slot);
        UiCommandResult DoubleClick(PlayerSlotRef slot);
        void LeftEnter(PlayerSlotRef slot);
        UiCommandResult RightEnter(PlayerSlotRef slot);
    }
}
