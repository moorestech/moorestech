using Client.Game.InGame.BlockInfoPolling;
using Core.Master;
using Server.Protocol.PacketResponse;

namespace Client.Ui.State.BlockInventory.Context
{
    /// <summary>
    /// 文脈の 1 欄の状態。判定は IsAbsent に揃える
    /// State of one context entry; showing follows the single IsAbsent check
    /// </summary>
    public abstract class ContextEntry<T>
    {
        public static readonly ContextEntry<T> Absent = new AbsentContextEntry<T>();

        public bool IsAbsent => this is AbsentContextEntry<T>;

        private protected ContextEntry()
        {
        }

        public static ContextEntry<T> Unavailable(BlockInfoFetchState fetchState)
        {
            return new UnavailableContextEntry<T>(fetchState);
        }

        public static ContextEntry<T> Fetched(T value)
        {
            return new FetchedContextEntry<T>(value);
        }
    }

    public sealed class AbsentContextEntry<T> : ContextEntry<T>
    {
        internal AbsentContextEntry()
        {
        }
    }

    public sealed class UnavailableContextEntry<T> : ContextEntry<T>
    {
        public BlockInfoFetchState FetchState { get; }

        internal UnavailableContextEntry(BlockInfoFetchState fetchState)
        {
            FetchState = fetchState;
        }
    }

    public sealed class FetchedContextEntry<T> : ContextEntry<T>
    {
        public T Value { get; }

        internal FetchedContextEntry(T value)
        {
            Value = value;
        }
    }

    // 搬入が止まった理由と、待っているアイテム
    // Why machine input is stalled, and the item it waits for
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
