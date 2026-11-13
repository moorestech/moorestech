using Client.Ui.Components.Slots;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// 枠の押下を型付きコマンドへ写しトーストへ渡す
    /// Maps slot gestures to commands and toasts the result
    /// </summary>
    public static class BlockSlotGestureCommands
    {
        public static void Run(IBlockSlotCommands commands, UiCommandResultToaster toaster, int blockSlot, SlotGesture gesture)
        {
            // 押下・右押下・ダブルクリックだけ反応する
            // React to press, right press, double click only
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
                    // silent-ok: 進入とホバーはブロックの枠では使わない（Web と同じ） / 名前のツールチップは ItemSlot が出し、掴みの分割ドラッグは C4-e が持ち録画 uitk-machine-ui で見る
                    return;
            }
        }
    }
}
