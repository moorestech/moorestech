using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身。液体は Fluids、他は ItemSlots
    /// Open car contents; Fluids for fluid cars, else ItemSlots
    /// </summary>
    public sealed class TrainInventoryBodyViewModel
    {
        public static readonly TrainInventoryBodyViewModel Closed = new(new List<SlotItem>(), new List<BlockFluidSlotState>());

        public IReadOnlyList<SlotItem> ItemSlots { get; }
        public IReadOnlyList<BlockFluidSlotState> Fluids { get; }

        public TrainInventoryBodyViewModel(IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids)
        {
            ItemSlots = itemSlots;
            Fluids = fluids;
        }
    }
}
