namespace Client.Game.InGame.BeltTransport.Statistics
{
    // 搬出統計が答えられる窓の長さ(tick)。90と180は5tickブロック36個のリングから、360以上は180tickブロックを2つ束ねた層から答える
    // Window lengths (ticks) the exit statistics can answer: 90 and 180 come from the ring of 36 five-tick blocks, 360 and up from layers pairing 180-tick blocks
    // 180はバニラ3速度の搬出周期(20・9・3tick)の最小公倍数で、満載時の個数が位相でぶれない最短の窓(ユーザー裁定 2026-10-20)
    // 180 is the least common multiple of the three vanilla output periods (20, 9 and 3 ticks): the shortest window whose full-belt count never wobbles with phase (user ruling 2026-10-20)
    public static class BeltSegmentExitWindows
    {
        public const int RingBlockTicks = 5;
        public const int RingSlots = 36;
        public const int RingTicks = RingBlockTicks * RingSlots;
        public const int PairLayerCount = 5;
        public const int DefaultWindowTicks = 180;

        public static readonly int[] Ticks = { 90, 180, 360, 720, 1440, 2880, 5760 };

        // リングから答える窓はリングの長さ以下。それより長い窓は、リング全体を1ブロックとする層の番号へ写す
        // A window up to the ring length is answered from the ring; a longer one maps to the layer whose block is the whole ring doubled per step
        public static bool IsRingWindow(int windowTicks)
        {
            return windowTicks <= RingTicks;
        }

        public static bool TryGetPairLayer(int windowTicks, out int layer)
        {
            for (layer = 0; layer < PairLayerCount; layer++)
            {
                if (PairWindowTicksOfLayer(layer) == windowTicks) return true;
            }
            layer = -1;
            return false;
        }

        public static int PairBlockTicksOfLayer(int layer)
        {
            return RingTicks << layer;
        }

        public static int PairWindowTicksOfLayer(int layer)
        {
            return PairBlockTicksOfLayer(layer) * 2;
        }
    }
}
