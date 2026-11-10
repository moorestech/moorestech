using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;

namespace Client.Ui.State.Train.Inventory
{
    /// <summary>
    /// 開いている貨車の中身。液体貨車は Fluids、それ以外は ItemSlots を描く
    /// Contents of the open car; fluid cars draw Fluids, others draw ItemSlots
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
