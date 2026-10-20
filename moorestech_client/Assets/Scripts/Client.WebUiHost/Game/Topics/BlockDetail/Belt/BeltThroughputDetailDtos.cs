using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// ベルトの搬出統計。窓ごとの搬出数と最大数を全窓ぶん、直近の1秒ごとの搬出数を棒グラフ用に載せ、どの窓を見せるかはWeb側が選ぶ
    /// Belt exit statistics: every window's exit count and maximum, plus the latest per-second counts for the bar chart; the Web side picks the window to show
    /// </summary>
    public class BeltThroughputDetailDto
    {
        // 1tickの進行量(1マス=360)。歯車ベルトは回転数で変わる
        // Advance per tick (one cell = 360); a gear belt's value follows the rotation
        public int CurrentSpeed;
        public List<BeltThroughputWindowDto> Windows;

        // 古い順に並ぶ直近の1秒(20tick)ごとの搬出数。埋まった秒だけを載せるので、起動直後は9個に満たない
        // Exit counts of the latest one-second (20-tick) buckets, oldest first; only filled seconds are listed, so fewer than 9 appear right after start
        public List<int> RecentSecondCounts;

        // 速度 × 20 ÷ 360。棒グラフの100%の高さ
        // Speed × 20 ÷ 360; the bar chart's full height
        public double MaxCountPerSecond;
    }

    public class BeltThroughputWindowDto
    {
        public int Ticks;

        // 窓がまだ埋まっていない(計測中)ならfalseで、ExitCountは0
        // False while the window is not yet filled (measuring), with ExitCount at 0
        public bool Measured;
        public int ExitCount;

        // 速度 × 窓 ÷ 360。バニラ速度ではどの窓でも整数になる
        // Speed × window ÷ 360; a whole number for every window at vanilla speeds
        public double MaxCount;
    }
}
