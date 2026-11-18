using Core.Master;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 搬入が止まった理由と待つアイテム
    /// Why input is stalled and the awaited item
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
