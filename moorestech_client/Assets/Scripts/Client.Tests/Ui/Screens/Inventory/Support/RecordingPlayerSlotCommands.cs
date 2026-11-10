using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.SlotCommands.Player;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    // 呼ばれた操作を書式付きで記録
    // Records calls in a fixed format
    public sealed class RecordingPlayerSlotCommands : IPlayerSlotCommands
    {
        public List<string> Calls { get; } = new();

        public UiCommandResult LeftDown(PlayerSlotRef slot, bool shift)
        {
            Calls.Add($"LeftDown {slot} shift={shift}");
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult RightDown(PlayerSlotRef slot)
        {
            Calls.Add($"RightDown {slot}");
            return UiCommandAccepted.Instance;
        }

        public UiCommandResult DoubleClick(PlayerSlotRef slot)
        {
            Calls.Add($"DoubleClick {slot}");
            return UiCommandAccepted.Instance;
        }

        public void LeftEnter(PlayerSlotRef slot)
        {
            Calls.Add($"LeftEnter {slot}");
        }

        public UiCommandResult RightEnter(PlayerSlotRef slot)
        {
            Calls.Add($"RightEnter {slot}");
            return UiCommandAccepted.Instance;
        }
    }
}
