namespace Tests.Util.Throughput
{
    // 計測窓の長さ。ユーザー案 361×8 を、全周期(2・3・9・20 tick)の公倍数 180 の倍数へ丸めた 2880
    // Measurement window length: the user's 361×8 rounded to 2880, a multiple of 180, the common multiple of every period (2, 3, 9, 20 ticks)
    // 周期の倍数なら、100% の流れはどの位相から数えても窓にちょうど W/P 個入る。分岐を含む配置も同じ窓(当初の 5760 は時間だけ倍で検出力は変わらないため 2026-10-18 に統一)
    // Being a multiple of the period, a 100% stream puts exactly W/P items in the window from any phase; splitter layouts use the same window (the former 5760 only doubled the time, unified on 2026-10-18)
    public static class BeltThroughputWindow
    {
        public const int Plain = 2880;

        public static int ExpectedTotal(int window, int periodTicks)
        {
            return window / periodTicks;
        }
    }
}
