namespace Game.MapGeneration.Pipeline.Tiling
{
    // 配置系の乱数種へタイル位置を混ぜる。混ぜないと全タイルが同じ Poisson 候補点列を回し、
    // 座標をワールド化しても候補点の格子だけが格子全体で反復して残る。
    // Mixes the tile's slot into the placement random seeds. Without it every tile runs the same Poisson
    // candidate series, and world-space coordinates alone still leave that candidate lattice repeating.
    public static class TileSeedMixer
    {
        public static int Mix(int seed, int tileIndexX, int tileIndexZ)
        {
            // System.Random は int.MinValue を受け取ると Math.Abs で溢れるため、最上位ビットを落として非負にする。
            // System.Random overflows in Math.Abs on int.MinValue, so the sign bit is dropped to keep the seed non-negative.
            unchecked
            {
                var hash = seed * 397;
                hash ^= (tileIndexX + 1) * 73856093;
                hash = hash * 397 ^ (tileIndexZ + 1) * 19349663;
                return hash & 0x7FFFFFFF;
            }
        }
    }
}
