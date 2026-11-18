using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    // alpha の格子から届くブロックの枠の押下を 1 行ずつ記録する（結果は返さない）
    // Records block-slot presses arriving from alpha's grid, one line each (no result is returned)
    public sealed class RecordingBlockSlotCommands : CommandRecorder<string>, IBlockSlotCommands
    {
        public void LeftDown(int blockSlot, bool shift) => Record($"LeftDown {blockSlot}{(shift ? " shift" : "")}");
        public void RightDown(int blockSlot) => Record($"RightDown {blockSlot}");
        public void DoubleClick(int blockSlot) => Record($"DoubleClick {blockSlot}");
    }
}
