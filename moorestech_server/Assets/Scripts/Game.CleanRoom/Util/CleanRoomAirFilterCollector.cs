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
    ///     部屋セルに重なる清浄機をブロック単位で重複なく集める（純度計算とUI照会で共有）
    ///     Collects air purifiers overlapping room cells once per block, shared by purity and UI queries
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

        // 純度計算と部屋の要約が共有する、清浄機の実効除去体積の合計
        // Sum of the purifiers' effective removal volumes, shared by the purity step and the room summary
        public static double SumRemovalVolume(IReadOnlyList<CleanRoomAirFilterEntry> entries)
        {
            var removalVolume = 0d;
            foreach (var entry in entries) removalVolume += entry.Filter.RemovalVolumePerSecond;
            return removalVolume;
        }
    }
}
