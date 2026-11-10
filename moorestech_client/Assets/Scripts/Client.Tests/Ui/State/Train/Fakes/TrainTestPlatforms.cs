using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 駅・品物 PF・液体 PF の開いた本文を組む
    /// Builds open station, item and fluid platform bodies
    /// </summary>
    public static class TrainTestPlatforms
    {
        public static OpenTrainPlatformBody StationBody(string syncedName, IReadOnlyList<SlotItem> slots, IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<string>.Present(syncedName), SectionContent<IReadOnlyList<SlotItem>>.Present(slots), fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }

        public static OpenTrainPlatformBody ItemPlatformBody(IReadOnlyList<SlotItem> slots, IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<string>.Absent, SectionContent<IReadOnlyList<SlotItem>>.Present(slots), fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }

        public static OpenTrainPlatformBody FluidPlatformBody(IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<string>.Absent, SectionContent<IReadOnlyList<SlotItem>>.Absent, fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }
    }
}
