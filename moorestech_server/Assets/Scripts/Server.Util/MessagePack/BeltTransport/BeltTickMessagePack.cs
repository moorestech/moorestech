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
        [Key(5)] public uint TickSequenceId { get; }
        [SerializationConstructor]
        public BeltTickMessagePack(ulong tick, BeltChangeMessagePack[] beforeTick, BeltOutputMessagePack[] outputs, BeltChangeMessagePack[] afterTick,
            uint serverTick, uint tickSequenceId)
        {
            Tick = tick;
            BeforeTick = beforeTick;
            Outputs = outputs;
            AfterTick = afterTick;
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
        }
        public BeltTickMessagePack(BeltTickDifference value, uint serverTick, uint tickSequenceId)
        {
            // 空の通知もtick完了を表し、前tickの受入結果を引き継がない。
            // Even an empty bundle completes a tick and does not reuse earlier acceptances.
            Tick = value.Tick; BeforeTick = value.BeforeTick.Select(BeltChangeMessagePack.FromCore).ToArray();
            Outputs = value.Outputs.Select(v => new BeltOutputMessagePack(v)).ToArray();
            AfterTick = value.AfterTick.Select(BeltChangeMessagePack.FromCore).ToArray();
            // 送信時に採番した共通tick・seqを付ける。
            // Attach the shared tick and sequence allocated when sending.
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
        }
        public BeltTickDifference ToCore() => new(Tick, BeforeTick.Select(v => v.ToCore()).ToArray(),
            Outputs.Select(v => v.ToCore()).ToArray(), AfterTick.Select(v => v.ToCore()).ToArray());
    }
}
