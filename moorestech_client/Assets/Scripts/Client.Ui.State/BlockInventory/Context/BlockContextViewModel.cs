using Client.Ui.State.BlockInventory.Common;
using Client.Ui.State.BlockInventory.Logic.CleanRoom;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 部屋・抽選・詰まりの 3 欄
    /// The three context entries; HasAny mirrors Web SectionStackView hasContextSections
    /// </summary>
    public sealed class BlockContextViewModel
    {
        public static readonly BlockContextViewModel None = new(
            SectionContent<CleanRoomFetchedDetail>.Absent,
            SectionContent<CleanRoomYieldFetched>.Absent,
            SectionContent<MachineInputStall>.Absent);

        public SectionContent<CleanRoomFetchedDetail> CleanRoom { get; }
        public SectionContent<CleanRoomYieldFetched> CleanRoomYield { get; }
        public SectionContent<MachineInputStall> MachineInputStall { get; }

        public bool HasAny => !CleanRoom.IsAbsent || !CleanRoomYield.IsAbsent || !MachineInputStall.IsAbsent;

        public BlockContextViewModel(SectionContent<CleanRoomFetchedDetail> cleanRoom, SectionContent<CleanRoomYieldFetched> cleanRoomYield, SectionContent<MachineInputStall> machineInputStall)
        {
            CleanRoom = cleanRoom;
            CleanRoomYield = cleanRoomYield;
            MachineInputStall = machineInputStall;
        }
    }
}
