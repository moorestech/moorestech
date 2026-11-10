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
            ContextEntry<CleanRoomFetchedDetail>.Absent,
            ContextEntry<CleanRoomYieldFetched>.Absent,
            ContextEntry<MachineInputStall>.Absent);

        public ContextEntry<CleanRoomFetchedDetail> CleanRoom { get; }
        public ContextEntry<CleanRoomYieldFetched> CleanRoomYield { get; }
        public ContextEntry<MachineInputStall> MachineInputStall { get; }

        public bool HasAny => !CleanRoom.IsAbsent || !CleanRoomYield.IsAbsent || !MachineInputStall.IsAbsent;

        public BlockContextViewModel(ContextEntry<CleanRoomFetchedDetail> cleanRoom, ContextEntry<CleanRoomYieldFetched> cleanRoomYield, ContextEntry<MachineInputStall> machineInputStall)
        {
            CleanRoom = cleanRoom;
            CleanRoomYield = cleanRoomYield;
            MachineInputStall = machineInputStall;
        }
    }
}
