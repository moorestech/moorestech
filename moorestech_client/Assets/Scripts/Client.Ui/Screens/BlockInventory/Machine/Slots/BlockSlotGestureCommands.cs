using Client.Ui.Components.Slots;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.BlockInventory.Machine.Slots
{
    /// <summary>
    /// 機械の枠の押下をブロック側の型付きコマンドへ写す（alpha BlockItemGrid と同じ写し: 左・右・ダブルだけ。進入は使わない）
    /// Maps machine slot gestures onto the typed block commands (same mapping as alpha BlockItemGrid: left, right, double only; no enter)
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
