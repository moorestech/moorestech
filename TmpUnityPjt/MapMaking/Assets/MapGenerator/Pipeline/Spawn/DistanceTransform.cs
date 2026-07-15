namespace MapGenerator.Pipeline.Spawn
{
    /// <summary>
    /// マスク内セルの境界距離変換と pole of inaccessibility 選定。純粋ロジック。
    /// </summary>
    public static class DistanceTransform
    {
        /// <summary>
        /// mask[i]==true のセルについて false セル（および配列外）までのチェビシェフ距離(セル単位)を返す。
        /// false セルは距離0。2パス(前方/後方)伝播の近似チェビシェフ距離。
        /// </summary>
        public static float[] ChebyshevToFalse(bool[] mask, int width, int height)
        {
            int n = width * height;
            var d = new float[n];
            float big = width + height + 2;
            for (int i = 0; i < n; i++) d[i] = mask[i] ? big : 0f;

            // 前方パス
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (d[i] == 0f) continue;
                    float m = d[i];
                    m = Min(m, Sample(d, x - 1, y, width, height) + 1f);
                    m = Min(m, Sample(d, x, y - 1, width, height) + 1f);
                    m = Min(m, Sample(d, x - 1, y - 1, width, height) + 1f);
                    m = Min(m, Sample(d, x + 1, y - 1, width, height) + 1f);
                    d[i] = m;
                }
            // 後方パス
            for (int y = height - 1; y >= 0; y--)
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = y * width + x;
                    if (d[i] == 0f) continue;
                    float m = d[i];
                    m = Min(m, Sample(d, x + 1, y, width, height) + 1f);
                    m = Min(m, Sample(d, x, y + 1, width, height) + 1f);
                    m = Min(m, Sample(d, x + 1, y + 1, width, height) + 1f);
                    m = Min(m, Sample(d, x - 1, y + 1, width, height) + 1f);
                    d[i] = m;
                }
            return d;
        }

        // 配列外は境界扱い(距離0)。これによりマスク端=境界になる。
        static float Sample(float[] d, int x, int y, int width, int height)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return 0f;
            return d[y * width + x];
        }

        static float Min(float a, float b) => a < b ? a : b;

        /// <summary>
        /// grassClearance/waterClearance(各セルの距離・物理m換算済み想定) から
        /// 両制約を満たすセルの中で min(grass, water) 最大のインデックスを返す。無ければ-1。
        /// </summary>
        public static int PickPole(float[] grassClearance, float[] waterClearance, int count,
            float grassMin, float waterMin)
        {
            int best = -1;
            float bestScore = -1f;
            for (int i = 0; i < count; i++)
            {
                float g = grassClearance[i];
                float w = waterClearance[i];
                if (g < grassMin || w < waterMin) continue;
                float s = g < w ? g : w;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best;
        }
    }
}
