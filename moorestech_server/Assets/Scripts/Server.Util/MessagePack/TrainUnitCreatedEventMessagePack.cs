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
        [Key(1)] public long ServerTick { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainUnitCreatedEventMessagePack()
        {
        }

        public TrainUnitCreatedEventMessagePack(TrainUnitSnapshotBundleMessagePack snapshot, long serverTick)
        {
            Snapshot = snapshot;
            ServerTick = serverTick;
        }
    }
}
