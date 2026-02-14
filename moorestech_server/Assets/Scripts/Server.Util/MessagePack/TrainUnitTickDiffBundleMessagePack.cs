using System;
using System.Collections.Generic;
using Game.Train.Unit;
using MessagePack;

namespace Server.Util.MessagePack
{
    // 1tick分のhash(n-1)とdiff(n)をまとめて送るペイロード
    // Payload that bundles hash(n-1) and diff(n) in one tick message.
    [MessagePackObject]
    public class TrainUnitTickDiffBundleMessagePack
    {
        [Key(0)] public uint ServerTick { get; set; }
        [Key(1)] public uint HashTickSequenceId { get; set; }
        [Key(2)] public uint DiffTickSequenceId { get; set; }
        [Key(3)] public uint UnitsHash { get; set; }
        [Key(4)] public uint RailGraphHash { get; set; }
        [Key(5)] public List<TrainUnitTickDiffMessagePack> Diffs { get; set; }

        [Obsolete("Reserved for MessagePack.")]
        public TrainUnitTickDiffBundleMessagePack()
        {
        }

        public TrainUnitTickDiffBundleMessagePack(
            uint serverTick,
            uint hashTickSequenceId,
            uint diffTickSequenceId,
            uint unitsHash,
            uint railGraphHash,
            IReadOnlyList<TrainUpdateService.TrainTickDiffData> diffs)
        {
            ServerTick = serverTick;
            HashTickSequenceId = hashTickSequenceId;
            DiffTickSequenceId = diffTickSequenceId;
            UnitsHash = unitsHash;
            RailGraphHash = railGraphHash;
            Diffs = BuildDiffs(diffs);
        }

        #region Internal

        private static List<TrainUnitTickDiffMessagePack> BuildDiffs(IReadOnlyList<TrainUpdateService.TrainTickDiffData> diffs)
        {
            var sourceDiffs = diffs ?? Array.Empty<TrainUpdateService.TrainTickDiffData>();
            var result = new List<TrainUnitTickDiffMessagePack>(sourceDiffs.Count);
            foreach (var diff in sourceDiffs)
            {
                result.Add(new TrainUnitTickDiffMessagePack(diff));
            }
            return result;
        }

        #endregion
    }
}
