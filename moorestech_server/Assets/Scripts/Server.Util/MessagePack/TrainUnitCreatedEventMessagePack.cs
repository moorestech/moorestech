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
        [Key(1)] public EntityMessagePack[] Entities { get; set; }
        [Key(2)] public long ServerTick { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainUnitCreatedEventMessagePack()
        {
        }

        public TrainUnitCreatedEventMessagePack(TrainUnitSnapshotBundleMessagePack snapshot, EntityMessagePack[] entities, long serverTick)
        {
            Snapshot = snapshot;
            Entities = entities;
            ServerTick = serverTick;
        }
    }
}
