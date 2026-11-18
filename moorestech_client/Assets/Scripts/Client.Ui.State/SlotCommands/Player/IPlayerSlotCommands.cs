using Client.Ui.State.Inventory.Actions;

namespace Client.Ui.State.SlotCommands.Player
{
    /// <summary>
    /// 持ち物・装備・ホットバーの枠の操作の口。結果は実装がトースト口へ渡す
    /// Port for inventory, equipment and hotbar slot gestures; the implementation hands results to the toaster
    /// </summary>
    public interface IPlayerSlotCommands
    {
        void LeftDown(PlayerSlotRef slot, bool shift);
        void RightDown(PlayerSlotRef slot);
        void DoubleClick(PlayerSlotRef slot);
        void LeftEnter(PlayerSlotRef slot);
        void RightEnter(PlayerSlotRef slot);
    }
}
