using Client.Ui.Components.Slots;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.BlockInventory.Machine.Slots
{
    /// <summary>
    /// 枠の押下を型付きコマンドへ写す
    /// Maps machine slot gestures onto typed block commands as alpha BlockItemGrid: left, right, double; no enter
    /// </summary>
    public static class BlockSlotGestureCommands
    {
        public static void Run(IBlockSlotCommands commands, UiCommandResultToaster toaster, int blockSlot, SlotGesture gesture)
        {
            switch (gesture.Kind)
            {
                case SlotGestureKind.LeftDown:
                    toaster.Report(BlockSlotCommands.CommandName, commands.LeftDown(blockSlot, gesture.Shift));
                    return;
                case SlotGestureKind.RightDown:
                    toaster.Report(BlockSlotCommands.CommandName, commands.RightDown(blockSlot));
                    return;
                case SlotGestureKind.DoubleClick:
                    toaster.Report(BlockSlotCommands.CommandName, commands.DoubleClick(blockSlot));
                    return;
                default:
                    // silent-ok: 進入とホバーはブロックの枠では使わない（Web と同じ） / 掴みの分割ドラッグは C4-e が持ち、録画 uitk-machine-ui で見る
                    return;
            }
        }
    }
}
