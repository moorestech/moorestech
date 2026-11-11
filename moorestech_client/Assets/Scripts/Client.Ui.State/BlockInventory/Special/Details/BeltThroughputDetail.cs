using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Belt;

namespace Client.Ui.State.BlockInventory.Special.Details
{
    /// <summary>
    /// ベルトの搬出統計（窓ごとの数と直近の秒ごとの数。Web BeltThroughputDetailDto と同じ値）
    /// Belt exit statistics: per-window counts and recent per-second counts (same values as Web BeltThroughputDetailDto)
    /// </summary>
    public sealed class BeltThroughputDetail
    {
        public IReadOnlyList<BeltThroughputWindow> Windows { get; }
        private IReadOnlyList<int> RecentSecondCounts { get; }
        private double MaxCountPerSecond { get; }

        public BeltThroughputDetail(IReadOnlyList<BeltThroughputWindow> windows, IReadOnlyList<int> recentSecondCounts, double maxCountPerSecond)
        {
            Windows = windows;
            RecentSecondCounts = recentSecondCounts;
            MaxCountPerSecond = maxCountPerSecond;
        }

        // 棒の高さ（古い順。足りない古い側は null で空の棒）
        // Bar heights, oldest first; the missing older side is null for empty bars
        public IReadOnlyList<float?> RecentSecondBars()
        {
            var ratios = BeltThroughputLogic.RecentSecondBars(RecentSecondCounts, MaxCountPerSecond, BeltThroughputLogic.RecentSecondBarCount);
            var bars = new float?[ratios.Length];
            for (var i = 0; i < ratios.Length; i++) bars[i] = ratios[i].HasValue ? (float)ratios[i].Value : null;
            return bars;
        }
    }
}
