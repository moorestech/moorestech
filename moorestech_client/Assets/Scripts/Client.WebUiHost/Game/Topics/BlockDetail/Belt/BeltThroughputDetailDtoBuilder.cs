using System.Collections.Generic;
using Client.Game.InGame.BeltTransport;
using Client.Game.InGame.BeltTransport.Statistics;
using Client.Game.InGame.Block;
using Core.BeltTransport;
using Mooresmaster.Model.BlocksModule;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// 開いたベルトのマスが属するsegmentの搬出統計をDTOへ写す。複製が未着、合流マス、ベルト以外では何も載せない
    /// Copies the exit statistics of the segment owning the opened belt's cell into the DTO; nothing for a missing replica, a merge cell or a non-belt
    /// </summary>
    public static class BeltThroughputDetailDtoBuilder
    {
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
            dto.BeltThroughput = new BeltThroughputDetailDto { CurrentSpeed = speed, Windows = windows };
        }
    }
}
