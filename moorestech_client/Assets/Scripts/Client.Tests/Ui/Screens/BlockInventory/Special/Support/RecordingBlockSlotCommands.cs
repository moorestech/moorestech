using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.Commands;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    // alpha の格子から届くブロックの枠の押下を 1 行ずつ記録する（結果は常に受理）
    // Records block-slot presses arriving from alpha's grid, one line each (always accepted)
    public sealed class RecordingBlockSlotCommands : CommandRecorder<string>, IBlockSlotCommands
    {
        public UiCommandResult LeftDown(int blockSlot, bool shift) => Reply($"LeftDown {blockSlot}{(shift ? " shift" : "")}");
        public UiCommandResult RightDown(int blockSlot) => Reply($"RightDown {blockSlot}");
        public UiCommandResult DoubleClick(int blockSlot) => Reply($"DoubleClick {blockSlot}");

        private UiCommandResult Reply(string line)
        {
            Record(line);
            return UiCommandAccepted.Instance;
        }
    }
}
