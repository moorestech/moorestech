using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;

namespace Client.Tests.Ui.State.Train.Fakes
{
    /// <summary>
    /// 駅・品物 PF・液体 PF の開いた本文を、Reader と同じ部分の有無で組む
    /// Builds open station, item platform and fluid platform bodies with the same part presence as the Reader
    /// </summary>
    public static class TrainTestPlatforms
    {
        public static OpenTrainPlatformBody StationBody(string syncedName, IReadOnlyList<SlotItem> slots, IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<TrainStationNamePart>.Present(new TrainStationNamePart(syncedName)), SectionContent<IReadOnlyList<SlotItem>>.Present(slots), fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }

        public static OpenTrainPlatformBody ItemPlatformBody(IReadOnlyList<SlotItem> slots, IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<TrainStationNamePart>.Absent, SectionContent<IReadOnlyList<SlotItem>>.Present(slots), fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }

        public static OpenTrainPlatformBody FluidPlatformBody(IReadOnlyList<BlockFluidSlotState> fluids, TrainPlatformTransferMode mode)
        {
            return new OpenTrainPlatformBody(SectionContent<TrainStationNamePart>.Absent, SectionContent<IReadOnlyList<SlotItem>>.Absent, fluids, SectionContent<TrainPlatformTransferMode>.Present(mode));
        }
    }
}
