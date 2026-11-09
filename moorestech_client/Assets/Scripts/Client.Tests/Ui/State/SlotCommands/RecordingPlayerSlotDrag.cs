using System.Collections.Generic;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.Inventory.Drag;

namespace Client.Tests.Ui.State.SlotCommands
{
    // 分割ドラッグの口の記録係
    // Recorder for the split-drag port
    internal sealed class RecordingPlayerSlotDrag : IPlayerSlotDragCommands
    {
        public readonly List<PlayerSlotRef> Begun = new();
        public readonly List<PlayerSlotRef> LeftEntered = new();
        public readonly List<PlayerSlotRef> RightEntered = new();

        public void BeginSplitDrag(PlayerSlotRef slot) => Begun.Add(slot);
        public void EnterWithLeftButton(PlayerSlotRef slot) => LeftEntered.Add(slot);

        public UiCommandResult EnterWithRightButton(PlayerSlotRef slot)
        {
            RightEntered.Add(slot);
            return UiCommandAccepted.Instance;
        }
    }
}
