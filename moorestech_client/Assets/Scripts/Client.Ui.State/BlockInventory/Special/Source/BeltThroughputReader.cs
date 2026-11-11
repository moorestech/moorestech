using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;
using Core.BeltTransport;
using Core.Update;
using Mooresmaster.Model.BlocksModule;

namespace Client.Ui.State.BlockInventory.Special.Source
{
    /// <summary>
    /// クライアントのベルトの複製が数えた搬出統計を読む（Web BeltThroughputDetailDtoBuilder の写し）
    /// Reads exit statistics counted by the client's belt replica (port of Web BeltThroughputDetailDtoBuilder)
    /// </summary>
    public static class BeltThroughputReader
    {
        private const int RingSlotsPerSecond = GameUpdater.TicksPerSecond / BeltSegmentExitWindows.RingBlockTicks;

        public static BeltThroughputDetail Read(BlockGameObject block, BeltTransportClientCache cache)
        {
            // ベルトでない・複製に観測区間が無いなら不在
            // Absent unless the block is a belt with an observed segment in the replica
            if (block.BlockMasterElement.BlockParam is not IBeltSpeedParam) return null;
            // silent-ok: ベルトの区間がまだ複製に載っていない間は区画を出さない（Web と同じ） / 載らないままなら録画（R-12）のベルトの段で気づく
            if (!cache.TryGetObservedSegmentIndex(block.BlockPosInfo.OriginalPos, out var segmentIndex)) return null;
            var speed = (double)cache.Replica.Segments[segmentIndex].Speed;

            // 窓ごとの数と最大（速さ×tick÷物の幅）
            // Per-window counts and maxima (speed x ticks / item width)
            var windows = new List<BeltThroughputWindow>(BeltSegmentExitWindows.Ticks.Length);
            foreach (var ticks in BeltSegmentExitWindows.Ticks)
            {
                var maxCount = speed * ticks / BeltConstants.ItemWidth;
                windows.Add(cache.ExitStatistics.TryGetExitCount(segmentIndex, ticks, out var exitCount) ? BeltThroughputWindow.Measured(ticks, exitCount, maxCount) : BeltThroughputWindow.Measuring(ticks, maxCount));
            }

            // 直近の秒ごとの数（埋まった分だけ）
            // Recent per-second counts, only as many as are filled
            var buckets = new int[BeltThroughputLogic.RecentSecondBarCount];
            var filled = cache.ExitStatistics.ReadRecentExitCounts(segmentIndex, RingSlotsPerSecond, buckets);
            var recent = new List<int>(filled);
            for (var i = 0; i < filled; i++) recent.Add(buckets[i]);
            return new BeltThroughputDetail(windows, recent, speed * GameUpdater.TicksPerSecond / BeltConstants.ItemWidth);
        }
    }
}
