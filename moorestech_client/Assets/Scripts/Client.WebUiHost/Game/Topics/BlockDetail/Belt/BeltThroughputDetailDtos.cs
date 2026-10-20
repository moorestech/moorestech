using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics.BlockDetail
{
    /// <summary>
    /// ベルトの搬出統計。窓ごとの搬出数と、その窓で速度どおりに流れた場合の最大数を全窓ぶん載せ、どの窓を見せるかはWeb側が選ぶ
    /// Belt exit statistics: every window's exit count and the maximum that window could carry at the current speed; the Web side picks the window to show
    /// </summary>
    public class BeltThroughputDetailDto
    {
        // 1tickの進行量(1マス=360)。歯車ベルトは回転数で変わる
        // Advance per tick (one cell = 360); a gear belt's value follows the rotation
        public int CurrentSpeed;
        public List<BeltThroughputWindowDto> Windows;
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
