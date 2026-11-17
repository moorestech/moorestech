using Client.Ui.State.Inventory.Panel;

namespace Client.Tests.Ui.State.Inventory.Panel
{
    public sealed class RecordingInventorySortCommands : IInventorySortCommands
    {
        public int SortCount { get; private set; }

        public void Sort()
        {
            SortCount++;
        }
    }
}
