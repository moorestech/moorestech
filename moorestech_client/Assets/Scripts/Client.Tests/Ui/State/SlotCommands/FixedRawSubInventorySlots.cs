using System.Collections.Generic;
using Client.Ui.State.Items;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Tests.Ui.State.SlotCommands
{
    /// <summary>
    /// 持たせた配列をそのまま返す生の値の偽物
    /// Fake raw slots returning the array it was given
    /// </summary>
    public sealed class FixedRawSubInventorySlots : IRawSubInventorySlots
    {
        private readonly IReadOnlyList<SlotItem> _slots;

        public FixedRawSubInventorySlots(IReadOnlyList<SlotItem> slots)
        {
            _slots = slots;
        }

        public IReadOnlyList<SlotItem> Read()
        {
            return _slots;
        }
    }
}
