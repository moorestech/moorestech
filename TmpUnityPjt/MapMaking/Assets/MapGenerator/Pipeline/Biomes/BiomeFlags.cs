namespace MapGenerator.Pipeline.Biomes
{
    /// <summary>
    /// 鉱石の出現バイオームを Unity の LayerMask のように複数選択するためのビットマスク。
    /// コンテンツバイオーム(Grassland以降)のみを対象とする。構造バイオーム(Ocean/Beach)は
    /// 鉱石の配置対象外のため含めない。
    /// </summary>
    [System.Flags]
    public enum BiomeFlags
    {
        None      = 0,
        Grassland = 1 << 0,
        Forest    = 1 << 1,
        Savanna   = 1 << 2,
        Desert    = 1 << 3,
        Mesa      = 1 << 4,
        Alpine    = 1 << 5,
        Jungle    = 1 << 6,
        Woods     = 1 << 7,
    }

    public static class BiomeFlagsExtensions
    {
        /// <summary>マスクに指定バイオームが含まれるか。</summary>
        public static bool Includes(this BiomeFlags mask, BiomeType biome)
        {
            return (mask & ToFlag(biome)) != BiomeFlags.None;
        }

        /// <summary>BiomeType を対応する単一ビットへ変換する。対象外(Ocean/Beach)は None。</summary>
        public static BiomeFlags ToFlag(BiomeType biome)
        {
            switch (biome)
            {
                case BiomeType.Grassland: return BiomeFlags.Grassland;
                case BiomeType.Forest:    return BiomeFlags.Forest;
                case BiomeType.Savanna:   return BiomeFlags.Savanna;
                case BiomeType.Desert:    return BiomeFlags.Desert;
                case BiomeType.Mesa:      return BiomeFlags.Mesa;
                case BiomeType.Alpine:    return BiomeFlags.Alpine;
                case BiomeType.Jungle:    return BiomeFlags.Jungle;
                case BiomeType.Woods:     return BiomeFlags.Woods;
                default:                  return BiomeFlags.None;
            }
        }
    }
}
