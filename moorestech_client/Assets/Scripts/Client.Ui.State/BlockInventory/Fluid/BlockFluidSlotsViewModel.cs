using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 追跡ブロックの液体の列と加工の進捗（進捗は機械・採掘機だけが持つ）
    /// The tracked block's fluid list and processing progress (only machines and miners have progress)
    /// </summary>
    public sealed class BlockFluidSlotsViewModel
    {
        public IReadOnlyList<BlockFluidSlotState> Slots { get; }
        public bool HasProgress { get; }
        public float Progress { get; }

        public BlockFluidSlotsViewModel(IReadOnlyList<BlockFluidSlotState> slots, bool hasProgress, float progress)
        {
            Slots = slots;
            HasProgress = hasProgress;
            Progress = progress;
        }
    }
}
