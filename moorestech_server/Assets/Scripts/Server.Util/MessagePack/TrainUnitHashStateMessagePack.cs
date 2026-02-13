using System;
using MessagePack;

namespace Server.Util.MessagePack
{
    // TrainUnit状態ハッシュの通知用メッセージ
    // Message payload for broadcasting train and rail hash/tick state
    [MessagePackObject]
    public class TrainUnitHashStateMessagePack
    {
        [Key(0)] public uint UnitsHash { get; set; }
        [Key(1)] public uint RailGraphHash { get; set; }
        [Key(2)] public uint ServerTick { get; set; }
        [Key(3)] public uint TickSequenceId { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainUnitHashStateMessagePack()
        {
        }

        public TrainUnitHashStateMessagePack(uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId)
        {
            UnitsHash = unitsHash;
            RailGraphHash = railGraphHash;
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
        }
    }
}
