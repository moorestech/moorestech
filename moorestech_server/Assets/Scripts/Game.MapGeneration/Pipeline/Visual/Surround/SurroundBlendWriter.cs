namespace Game.MapGeneration.Pipeline.Visual.Surround
{
    /// <summary>
    ///     1画素ぶんの重みを裸地レイヤーへ寄せる。他レイヤーを(1-blend)倍してから元の合計のblend割合を足すので、
    ///     書き込み後の合計は S+blend*m になる（S は元の合計、m は書き込み先レイヤーの元の重み）。
    ///     合計が保たれるのは m=0 の画素だけで、既に重みがある画素や重なる岩の2回目以降は1を超える。
    ///     移植元 TerrainGenerator.cs:1650-1660 / :1699-1706 と同一の畳み方で、この性質もそのまま引き継いでいる
    ///     Shifts one pixel's weight onto the bare-ground layer; the other layers scale by (1-blend) before the
    ///     blended share of the original total is added, leaving the sum at S + blend*m (S the original total,
    ///     m the target layer's original weight). Only an m = 0 pixel keeps its sum; one that already carries
    ///     weight, or a second write from overlapping rocks, ends above 1.
    ///     This is the same fold as the source's TerrainGenerator.cs:1650-1660 / :1699-1706, inherited property included
    /// </summary>
    public static class SurroundBlendWriter
    {
        public static void Blend(float[,,] alphamap, int pixelZ, int pixelX, int layerIndex, float blend)
        {
            var layerCount = alphamap.GetLength(2);

            // 合計0の画素に足すと1画素だけ突出した重みになる。移植元と同じく触らずに抜ける
            // Adding to a pixel that sums to zero would spike one lone weight; as in the source it is left alone
            var total = 0f;
            for (var layer = 0; layer < layerCount; layer++) total += alphamap[pixelZ, pixelX, layer];
            if (total < 0.001f) return;

            var remaining = 1f - blend;
            for (var layer = 0; layer < layerCount; layer++)
            {
                if (layer == layerIndex) continue;
                alphamap[pixelZ, pixelX, layer] *= remaining;
            }

            alphamap[pixelZ, pixelX, layerIndex] += blend * total;
        }
    }
}
