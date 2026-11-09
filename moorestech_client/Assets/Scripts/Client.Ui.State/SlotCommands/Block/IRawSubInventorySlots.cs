using System.Collections.Generic;
using Client.Ui.State.Items;

namespace Client.Ui.State.SlotCommands.Block
{
    /// <summary>
    /// サブインベントリの枠の生の値（コントローラの先書き込みを含む）を押下の瞬間に読む口
    /// Port reading the raw sub-inventory slots (including the controller's local pre-write) at press time
    /// </summary>
    public interface IRawSubInventorySlots
    {
        IReadOnlyList<SlotItem> Read();
    }
}
