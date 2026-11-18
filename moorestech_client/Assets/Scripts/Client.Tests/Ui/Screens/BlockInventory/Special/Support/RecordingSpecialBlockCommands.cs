using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Commands;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// 特殊族の操作を Web の action と payload の形の 1 行で記録する（結果のトーストは SpecialBlockCommandsTest が見る）
    /// Records each special command as one line shaped like the Web action and payload (result toasts are covered by SpecialBlockCommandsTest)
    /// </summary>
    public sealed class RecordingSpecialBlockCommands : CommandRecorder<string>, ISpecialBlockCommands
    {
        public void SetFilterMode(int directionIndex, FilterSplitterMode mode) => Record($"filter_splitter.set_mode {directionIndex} {mode}");
        public void SetFilterItem(int directionIndex, int slotIndex, bool clear) => Record($"filter_splitter.set_filter_item {directionIndex} {slotIndex} {(clear ? "clear" : "set")}");
        public void SetElectricToGearMode(int modeIndex) => Record($"electric_to_gear.set_output_mode {modeIndex}");
        public void ConnectSpaceElevator() => Record("space_elevator.connect");
        public void SendRocketCommand(RocketCommand command) => Record($"rocket_launch_pad.command {command}");
    }
}
