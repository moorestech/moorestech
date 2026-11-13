using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom.Util
{
    public readonly struct CleanRoomAirFilterEntry
    {
        public readonly IBlock Block;
        public readonly ICleanRoomAirFilter Filter;

        public CleanRoomAirFilterEntry(IBlock block, ICleanRoomAirFilter filter)
        {
            Block = block;
            Filter = filter;
        }
    }

    /// <summary>
    ///     部屋内の清浄機を重複なく集める
    ///     Collects air purifiers overlapping room cells once per block, shared by the class decision and UI queries
    /// </summary>
    public static class CleanRoomAirFilterCollector
    {
        public static List<CleanRoomAirFilterEntry> Collect(CleanRoom room, IWorldBlockDatastore world)
        {
            var entries = new List<CleanRoomAirFilterEntry>();
            var visited = new HashSet<BlockInstanceId>();
            foreach (var cell in room.Cells)
            {
                if (!world.TryGetBlock(cell, out var block)) continue;
                if (!visited.Add(block.BlockInstanceId)) continue;
                if (block.TryGetComponent<ICleanRoomAirFilter>(out var filter)) entries.Add(new CleanRoomAirFilterEntry(block, filter));
            }

            return entries;
        }

        // クラス判定と部屋の要約が共有する換気回数（除去体積合計÷部屋体積）
        // Air change rate (total removal volume / room volume) shared by the class decision and the room summary
        public static double AirChangeRate(CleanRoom room, IReadOnlyList<CleanRoomAirFilterEntry> entries)
        {
            return CleanRoomPurityLogic.AirChangeRate(SumRemovalVolume(entries), room.Volume);
        }

        private static double SumRemovalVolume(IReadOnlyList<CleanRoomAirFilterEntry> entries)
        {
            var removalVolume = 0d;
            foreach (var entry in entries) removalVolume += entry.Filter.RemovalVolumePerSecond;
            return removalVolume;
        }
    }
}
