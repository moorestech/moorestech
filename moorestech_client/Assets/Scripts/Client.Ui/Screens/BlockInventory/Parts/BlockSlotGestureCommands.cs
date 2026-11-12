using Client.Ui.Components.Slots;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// ブロックの枠の押下を型付きコマンドへ写し、結果をトーストへ渡す（Web useBlockSlotGestures。BlockItemGrid と機械の帯が共有する）
    /// Maps block slot gestures onto typed block commands and reports the result to the toaster (Web useBlockSlotGestures; shared by BlockItemGrid and the machine bands)
    /// </summary>
    public static class BlockSlotGestureCommands
    {
        public static void Run(IBlockSlotCommands commands, UiCommandResultToaster toaster, int blockSlot, SlotGesture gesture)
        {
            // ブロックの枠は押下・右押下・ダブルクリックだけ（Web と同じ）
            // Block slots react to press, right press and double click only (same as the Web)
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
