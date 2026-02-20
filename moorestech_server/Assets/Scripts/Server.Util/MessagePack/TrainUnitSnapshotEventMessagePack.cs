using System;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    // TrainUnit単機スナップショットイベントのペイロード
    // Payload for per-train-unit snapshot events.
    [MessagePackObject]
    public class TrainUnitSnapshotEventMessagePack
    {
        [Key(0)] public TrainInstanceId TrainInstanceId { get; set; }
        [Key(1)] public bool IsDeleted { get; set; }
        [Key(2)] public TrainUnitSnapshotBundleMessagePack Snapshot { get; set; }
        [Key(3)] public uint ServerTick { get; set; }
        [Key(4)] public uint TickSequenceId { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainUnitSnapshotEventMessagePack()
        {
        }

        public TrainUnitSnapshotEventMessagePack(
            TrainInstanceId trainInstanceId,
            bool isDeleted,
            TrainUnitSnapshotBundleMessagePack snapshot,
            uint serverTick,
            uint tickSequenceId)
        {
            TrainInstanceId = trainInstanceId;
            IsDeleted = isDeleted;
            Snapshot = snapshot;
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
        }
    }
}
