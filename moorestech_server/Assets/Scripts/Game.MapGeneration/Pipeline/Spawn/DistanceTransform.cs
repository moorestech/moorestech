namespace Game.MapGeneration.Pipeline.Spawn
{
    // マスク内セルの境界距離変換と pole of inaccessibility 選定。純粋ロジック。
    // Boundary distance transform over a mask plus pole-of-inaccessibility selection; pure logic.
    public static class DistanceTransform
    {
        public static float[] ChebyshevToFalse(bool[] mask, int width, int height)
        {
            int n = width * height;
            var d = new float[n];
            float big = width + height + 2;
            for (int i = 0; i < n; i++) d[i] = mask[i] ? big : 0f;

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

        static float Sample(float[] d, int x, int y, int width, int height)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return 0f;
            return d[y * width + x];
        }

        static float Min(float a, float b) => a < b ? a : b;

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
