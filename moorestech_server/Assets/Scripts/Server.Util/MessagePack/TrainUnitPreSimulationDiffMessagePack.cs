using System;
using System.Collections.Generic;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    // TrainUnit pre-simulation diff payload
    // Payload for pre-simulation TrainUnit diff notifications.
    [MessagePackObject]
    public class TrainUnitPreSimulationDiffMessagePack
    {
        [Key(0)] public uint ServerTick { get; set; }
        [Key(1)] public List<TrainUnitTickDiffMessagePack> Diffs { get; set; }
        [Key(2)] public uint TickSequenceId { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainUnitPreSimulationDiffMessagePack()
        {
        }

        public TrainUnitPreSimulationDiffMessagePack(uint serverTick, uint tickSequenceId, IReadOnlyList<TrainUpdateService.TrainTickDiffData> diffs)
        {
            ServerTick = serverTick;
            TickSequenceId = tickSequenceId;
            var sourceDiffs = diffs ?? Array.Empty<TrainUpdateService.TrainTickDiffData>();
            Diffs = new List<TrainUnitTickDiffMessagePack>(sourceDiffs.Count);
            foreach (var diff in sourceDiffs)
            {
                Diffs.Add(new TrainUnitTickDiffMessagePack(diff));
            }
        }
    }

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

