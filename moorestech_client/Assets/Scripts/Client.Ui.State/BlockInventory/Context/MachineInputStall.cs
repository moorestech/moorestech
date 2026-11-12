using Core.Master;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 搬入が止まった理由と、待っているアイテム（文脈の詰まりの欄の値）
    /// Why machine input is stalled, and the item it waits for (the value of the context stall entry)
    /// </summary>
    public sealed class MachineInputStall
    {
        public MachineInputStallReasonKind Reason { get; }
        public ItemId WaitingItemId { get; }

        public MachineInputStall(MachineInputStallReasonKind reason, ItemId waitingItemId)
        {
            Reason = reason;
            WaitingItemId = waitingItemId;
        }
    }
}
