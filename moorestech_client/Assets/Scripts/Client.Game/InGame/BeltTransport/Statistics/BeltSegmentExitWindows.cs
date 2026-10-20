namespace Client.Game.InGame.BeltTransport.Statistics
{
    // 搬出統計が答えられる窓の長さ(tick)。45tickブロックを2つ束ねた90から、層を1つ上がるごとに倍になる
    // Window lengths (ticks) the exit statistics can answer: 90 from two 45-tick blocks, doubling with each layer above
    // 180はバニラ3速度の搬出周期(20・9・3tick)の最小公倍数で、満載時の個数が位相でぶれない最短の窓(ユーザー裁定 2026-10-20)
    // 180 is the least common multiple of the three vanilla output periods (20, 9 and 3 ticks): the shortest window whose full-belt count never wobbles with phase (user ruling 2026-10-20)
    public static class BeltSegmentExitWindows
    {
        public const int PairBaseBlockTicks = 45;
        public const int LayerCount = 7;
        public const int DefaultWindowTicks = 180;

        public static readonly int[] Ticks = { 90, 180, 360, 720, 1440, 2880, 5760 };

        // 窓の長さから、その窓を2スロットで持つ層の番号を引く
        // Resolve the layer that holds the window as its two slots from the window length
        public static bool TryGetLayer(int windowTicks, out int layer)
        {
            for (layer = 0; layer < Ticks.Length; layer++)
            {
                if (Ticks[layer] == windowTicks) return true;
            }
            layer = -1;
            return false;
        }

        public static int BlockTicksOfLayer(int layer)
        {
            return PairBaseBlockTicks << layer;
        }
    }
}
