using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Items;

namespace Client.Ui.State.Train.Platform
{
    /// <summary>
    /// 駅・プラットフォームの本文の状態。閉じているか、部分ごとの有無を持つ開いた本文
    /// State of the station and platform body: closed, or open with each part present or absent
    /// </summary>
    public abstract class TrainPlatformBodyState
    {
        public static readonly TrainPlatformBodyState Closed = new ClosedTrainPlatformBody();
    }

    public sealed class ClosedTrainPlatformBody : TrainPlatformBodyState
    {
        internal ClosedTrainPlatformBody() { }
    }

    public sealed class OpenTrainPlatformBody : TrainPlatformBodyState
    {
        // 駅だけが持つ。他は Absent
        // Present on stations only; Absent otherwise
        public SectionContent<TrainStationNamePart> StationName { get; }
        // 移送の状態がある品物 PF・駅だけが持つ。他は Absent
        // Present only on item platforms and stations with a transfer state; Absent otherwise
        public SectionContent<IReadOnlyList<SlotItem>> ItemSlots { get; }
        public IReadOnlyList<BlockFluidSlotState> Fluids { get; }
        // 移送の状態があるときだけ持つ
        // Present only when a transfer state exists
        public SectionContent<TrainPlatformTransferMode> Mode { get; }

        public OpenTrainPlatformBody(SectionContent<TrainStationNamePart> stationName, SectionContent<IReadOnlyList<SlotItem>> itemSlots, IReadOnlyList<BlockFluidSlotState> fluids, SectionContent<TrainPlatformTransferMode> mode)
        {
            StationName = stationName;
            ItemSlots = itemSlots;
            Fluids = fluids;
            Mode = mode;
        }
    }
}
