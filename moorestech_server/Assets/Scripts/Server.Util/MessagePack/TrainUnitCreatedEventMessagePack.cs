using System;
using MessagePack;

namespace Server.Util.MessagePack
{
    // TrainUnit生成イベントのペイロード
    // Payload for train unit creation events
    [MessagePackObject]
    public class TrainUnitCreatedEventMessagePack
    {
        [Key(0)] public TrainUnitSnapshotBundleMessagePack Snapshot { get; set; }
        [Key(1)] public uint ServerTick { get; set; }
        [Key(2)] public uint TickSequenceId { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainUnitCreatedEventMessagePack()
        {
        }

        public TrainUnitCreatedEventMessagePack(TrainUnitSnapshotBundleMessagePack snapshot, uint serverTick, uint tickSequenceId)
        {
            Snapshot = snapshot;
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
        }
    }
}
