namespace Tests.Util.Throughput
{
    // 計測窓の長さ。分岐を含む配置は round robin で受けチェストあたりの個数が減るので長くする（ユーザー裁定 2026-10-18）
    // Measurement window lengths; layouts with splitters get a longer one because round robin thins each sink's count (user ruling 2026-10-18)
    public static class BeltThroughputWindow
    {
        private const int CellLength = 360;
        public const int Plain = (CellLength + 1) * 8;
        public const int WithSplitter = (CellLength + 1) * 16;

        // 窓の開始は着荷の tick に揃うので、以後は周期ごとに1個ずつ届き、個数は整数除算でちょうど決まる
        // The window opens on an arrival tick, so later arrivals come once per period and the count is exactly the integer division
        public static int ExpectedTotal(int window, int periodTicks)
        {
            return window / periodTicks;
        }
    }
}
