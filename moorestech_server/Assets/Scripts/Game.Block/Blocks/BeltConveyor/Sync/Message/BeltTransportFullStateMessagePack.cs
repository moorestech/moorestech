using System;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // ベルト搬送の全量の通信形。再構築tickの送信では連番を消費し、途中参加の送信では発行済み最新の連番をウォーターマークとして載せる
    // Wire form of the belt transport full state; a rebuild-tick send consumes a sequence id, a join-time send carries the latest issued id as a watermark
    [MessagePackObject]
    public class BeltTransportFullStateMessagePack
    {
        [Key(0)] public uint ServerTick { get; set; }
        [Key(1)] public uint TickSequenceId { get; set; }
        [Key(2)] public BeltSegmentStateMessagePack[] Segments { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltTransportFullStateMessagePack() { }

        public BeltTransportFullStateMessagePack(uint serverTick, uint tickSequenceId, BeltTransportFullState fullState)
        {
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
            Segments = new BeltSegmentStateMessagePack[fullState.Segments.Length];
            for (var i = 0; i < Segments.Length; i++) Segments[i] = new BeltSegmentStateMessagePack(fullState.Segments[i]);
        }

        public BeltTransportFullState ToFullState()
        {
            var segments = new BeltSegmentState[Segments.Length];
            for (var i = 0; i < segments.Length; i++) segments[i] = Segments[i].ToState();
            return new BeltTransportFullState(segments);
        }
    }
}
