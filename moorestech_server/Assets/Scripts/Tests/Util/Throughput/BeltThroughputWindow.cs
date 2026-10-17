namespace Tests.Util.Throughput
{
    // 計測窓の長さ。ユーザー案 361×8 / 361×16 を、全周期(2・3・9・20 tick)の公倍数 180 の倍数へ丸めた 2880 / 5760
    // Measurement window lengths: the user's 361×8 / 361×16 rounded to 2880 / 5760, multiples of 180, the common multiple of every period (2, 3, 9, 20 ticks)
    // 周期の倍数なら、100% の流れはどの位相から数えても窓にちょうど W/P 個入る。分岐を含む配置は round robin で薄まるので長い方
    // Being a multiple of the period, a 100% stream puts exactly W/P items in the window from any phase; layouts with splitters take the longer one as round robin thins each path
    public static class BeltThroughputWindow
    {
        public const int Plain = 2880;
        public const int WithSplitter = 5760;

        public static int ExpectedTotal(int window, int periodTicks)
        {
            return window / periodTicks;
        }
    }
}
