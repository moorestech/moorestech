using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Panel;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public sealed class RecordingInventorySortCommands : IInventorySortCommands
    {
        public int SortCount { get; private set; }

        public UiCommandResult Sort()
        {
            SortCount++;
            return UiCommandAccepted.Instance;
        }
    }
}
