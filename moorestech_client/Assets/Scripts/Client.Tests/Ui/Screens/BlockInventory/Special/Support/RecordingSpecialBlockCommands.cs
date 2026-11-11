using Client.Tests.Ui.TestDoubles;
using Client.Ui.State.BlockInventory.Logic.Special;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Cysharp.Threading.Tasks;

namespace Client.Tests.Ui.Screens.BlockInventory.Special.Support
{
    /// <summary>
    /// 特殊族の操作を Web の action と payload の形の 1 行で記録し、テストが決めた結果を返す（既定は受理）
    /// Records each special command as one line shaped like the Web action and payload, returning the outcome chosen by the test (accepted by default)
    /// </summary>
    public sealed class RecordingSpecialBlockCommands : CommandRecorder<string>, ISpecialBlockCommands
    {
        private UiCommandResult _next = UiCommandAccepted.Instance;

        public void SetNextResult(UiCommandResult result)
        {
            _next = result;
        }

        public UniTask<UiCommandResult> SetFilterMode(int directionIndex, FilterSplitterMode mode) => Reply($"filter_splitter.set_mode {directionIndex} {mode}");
        public UniTask<UiCommandResult> SetFilterItem(int directionIndex, int slotIndex, bool clear) => Reply($"filter_splitter.set_filter_item {directionIndex} {slotIndex} {(clear ? "clear" : "set")}");
        public UniTask<UiCommandResult> SetElectricToGearMode(int modeIndex) => Reply($"electric_to_gear.set_output_mode {modeIndex}");
        public UniTask<UiCommandResult> ConnectSpaceElevator() => Reply("space_elevator.connect");
        public UniTask<UiCommandResult> SendRocketCommand(RocketCommand command) => Reply($"rocket_launch_pad.command {command}");

        private UniTask<UiCommandResult> Reply(string line)
        {
            Record(line);
            return UniTask.FromResult(_next);
        }
    }
}
