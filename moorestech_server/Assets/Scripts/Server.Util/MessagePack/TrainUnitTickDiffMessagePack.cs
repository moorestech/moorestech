using System;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    // TrainUnitの1tick差分を表すメッセージ
    // Message that represents per-tick TrainUnit diff.
    [MessagePackObject]
    public class TrainUnitTickDiffMessagePack
    {
        [Key(0)] public Guid TrainId { get; set; }
        [Key(1)] public int MasconLevelDiff { get; set; }
        [Key(2)] public bool IsNowDockingSpeedZero { get; set; }
        [Key(3)] public int ApproachingNodeIdDiff { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainUnitTickDiffMessagePack()
        {
        }

        public TrainUnitTickDiffMessagePack(TrainUpdateService.TrainTickDiffData diff)
        {
            TrainId = diff.TrainId;
            MasconLevelDiff = diff.MasconLevelDiff;
            IsNowDockingSpeedZero = diff.IsNowDockingSpeedZero;
            ApproachingNodeIdDiff = diff.ApproachingNodeIdDiff;
        }
    }
}
