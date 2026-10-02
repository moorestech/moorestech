using System.Linq;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltTickMessagePack
    {
        [Key(0)] public ulong Tick { get; }
        [Key(1)] public BeltChangeMessagePack[] BeforeTick { get; }
        [Key(2)] public BeltOutputMessagePack[] Outputs { get; }
        [Key(3)] public BeltChangeMessagePack[] AfterTick { get; }
        [Key(4)] public uint ServerTick { get; }
        [Key(5)] public uint[] BeforeSequenceIds { get; }
        [Key(6)] public uint SimulationSequenceId { get; }
        [Key(7)] public uint[] AfterSequenceIds { get; }
        [Key(8)] public uint CompletedSequenceId { get; }
        [SerializationConstructor]
        public BeltTickMessagePack(ulong tick, BeltChangeMessagePack[] beforeTick, BeltOutputMessagePack[] outputs, BeltChangeMessagePack[] afterTick,
            uint serverTick, uint[] beforeSequenceIds, uint simulationSequenceId, uint[] afterSequenceIds, uint completedSequenceId)
        {
            Tick = tick;
            BeforeTick = beforeTick;
            Outputs = outputs;
            AfterTick = afterTick;
            ServerTick = serverTick;
            BeforeSequenceIds = beforeSequenceIds;
            SimulationSequenceId = simulationSequenceId;
            AfterSequenceIds = afterSequenceIds;
            CompletedSequenceId = completedSequenceId;
        }
        public BeltTickMessagePack(BeltTickDifference value)
        {
            // 空の通知もtick完了を表し、前tickの受入結果を引き継がない。
            // Even an empty bundle completes a tick and does not reuse earlier acceptances.
            Tick = value.Tick; BeforeTick = value.BeforeTick.Select(BeltChangeMessagePack.FromCore).ToArray();
            Outputs = value.Outputs.Select(v => new BeltOutputMessagePack(v)).ToArray();
            AfterTick = value.AfterTick.Select(BeltChangeMessagePack.FromCore).ToArray();
            // 搬送のtickと既存同期のtick・seqを対応させる。
            // Map the transport tick onto the existing synchronization tick and sequences.
            ServerTick = value.Order.ServerTick;
            BeforeSequenceIds = value.Order.BeforeSequenceIds;
            SimulationSequenceId = value.Order.SimulationSequenceId;
            AfterSequenceIds = value.Order.AfterSequenceIds;
            CompletedSequenceId = value.Order.CompletedSequenceId;
        }
        public BeltTickDifference ToCore() => new(Tick, BeforeTick.Select(v => v.ToCore()).ToArray(),
            Outputs.Select(v => v.ToCore()).ToArray(), AfterTick.Select(v => v.ToCore()).ToArray(),
            new BeltTickOrder(ServerTick, BeforeSequenceIds, SimulationSequenceId, AfterSequenceIds, CompletedSequenceId));
    }
}
