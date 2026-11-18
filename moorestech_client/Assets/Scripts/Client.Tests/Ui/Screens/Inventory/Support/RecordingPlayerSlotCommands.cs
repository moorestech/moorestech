using System.Collections.Generic;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.SlotCommands.Player;

namespace Client.Tests.Ui.Screens.Inventory.Support
{
    // 呼ばれた操作を書式付きで記録
    // Records calls in a fixed format
    public sealed class RecordingPlayerSlotCommands : IPlayerSlotCommands
    {
        public List<string> Calls { get; } = new();

        public void LeftDown(PlayerSlotRef slot, bool shift)
        {
            Calls.Add($"LeftDown {slot} shift={shift}");
        }

        public void RightDown(PlayerSlotRef slot)
        {
            Calls.Add($"RightDown {slot}");
        }

        public void DoubleClick(PlayerSlotRef slot)
        {
            Calls.Add($"DoubleClick {slot}");
        }

        public void LeftEnter(PlayerSlotRef slot)
        {
            Calls.Add($"LeftEnter {slot}");
        }

        public void RightEnter(PlayerSlotRef slot)
        {
            Calls.Add($"RightEnter {slot}");
        }
    }
}
