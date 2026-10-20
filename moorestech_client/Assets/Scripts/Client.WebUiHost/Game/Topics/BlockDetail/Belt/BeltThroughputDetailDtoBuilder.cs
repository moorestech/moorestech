using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Core.BeltTransport;
using Core.Update;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 開いたベルトのマスが属するsegmentの搬出統計をDTOへ写す。複製が未着、合流マス、ベルト以外では何も載せない
    /// Copies the exit statistics of the segment owning the opened belt's cell into the DTO; nothing for a missing replica, a merge cell or a non-belt
    /// </summary>
    public static class BeltThroughputDetailDtoBuilder
    {
        // 棒グラフは1秒(20tick)の束を9本。リングの5tickスロット4個で1秒
        // The bar chart shows nine one-second (20-tick) bundles; four 5-tick ring slots make a second
        public const int RecentSecondCount = 9;
        private const int RingSlotsPerSecond = GameUpdater.TicksPerSecond / BeltSegmentExitWindows.RingBlockTicks;

        public static void Apply(BlockInventoryDto dto, BlockGameObject block, object param, BeltTransportClientCache cache)
        {
            if (param is not IBeltSpeedParam) return;
            if (!cache.TryGetObservedSegmentIndex(block.BlockPosInfo.OriginalPos, out var segmentIndex)) return;

            var speed = cache.Replica.Segments[segmentIndex].Speed;
            var windows = new List<BeltThroughputWindowDto>(BeltSegmentExitWindows.Ticks.Length);
            foreach (var ticks in BeltSegmentExitWindows.Ticks)
            {
                var measured = cache.ExitStatistics.TryGetExitCount(segmentIndex, ticks, out var exitCount);
                windows.Add(new BeltThroughputWindowDto
                {
                    Ticks = ticks,
                    Measured = measured,
                    ExitCount = exitCount,
                    MaxCount = (double)speed * ticks / BeltConstants.ItemWidth,
                });
            }

            // 埋まった秒だけを古い順に載せる
            // List only the filled seconds, oldest first
            var buckets = new int[RecentSecondCount];
            var filled = cache.ExitStatistics.ReadRecentExitCounts(segmentIndex, RingSlotsPerSecond, buckets);
            var recentSecondCounts = new List<int>(filled);
            for (var i = 0; i < filled; i++) recentSecondCounts.Add(buckets[i]);

            dto.BeltThroughput = new BeltThroughputDetailDto
            {
                CurrentSpeed = speed,
                Windows = windows,
                RecentSecondCounts = recentSecondCounts,
                MaxCountPerSecond = (double)speed * GameUpdater.TicksPerSecond / BeltConstants.ItemWidth,
            };
        }
    }
}
