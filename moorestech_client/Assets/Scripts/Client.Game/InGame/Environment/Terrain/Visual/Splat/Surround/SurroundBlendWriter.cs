namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     1画素ぶんの重みを裸地レイヤーへ寄せる。他レイヤーを(1-blend)倍してから元の合計のblend割合を足すので、
    ///     元が合計1の画素は1のまま保たれる（移植元 TerrainGenerator.cs:1650-1660 / :1699-1706 と同一の畳み方）
    ///     Shifts one pixel's weight onto the bare-ground layer; the other layers scale by (1-blend) before the
    ///     blended share of the original total is added, so a pixel summing to 1 still sums to 1
    ///     (the same fold as the source's TerrainGenerator.cs:1650-1660 / :1699-1706)
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
