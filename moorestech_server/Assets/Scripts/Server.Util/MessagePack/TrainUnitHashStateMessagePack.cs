using System;
using MessagePack;

namespace Server.Util.MessagePack
{
    // TrainUnit状態ハッシュの通知用メッセージ
    // Message payload for broadcasting TrainUnit hash/tick state
    [MessagePackObject]
    public class TrainUnitHashStateMessagePack
    {
        [Key(0)] public uint UnitsHash { get; set; }
        [Key(1)] public long ServerTick { get; set; }

        [Obsolete("Reserved for MessagePack serialization.")]
        public TrainUnitHashStateMessagePack()
        {
        }

        public TrainUnitHashStateMessagePack(uint unitsHash, long serverTick)
        {
            UnitsHash = unitsHash;
            ServerTick = serverTick;
        }
    }
}
