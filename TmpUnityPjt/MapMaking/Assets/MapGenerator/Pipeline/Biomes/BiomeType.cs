namespace MapGenerator.Pipeline.Biomes
{
    /// <summary>
    /// Ocean(0)とBeach(1)は構造バイオーム（landMaskから自動決定）。
    /// Grassland以降がコンテンツバイオーム（IBiomeDefinitionで実装）。
    /// biomeWeightsの列順序と対応: [0]=Ocean, [1]=Beach, [2+]=コンテンツバイオーム。
    /// </summary>
    public enum BiomeType
    {
        Ocean = 0,
        Beach = 1,
        Grassland = 2,
        Forest = 3,
        Savanna = 4,
        Desert = 5,
        Mesa = 6,
        Alpine = 7,
        Jungle = 8,
        Woods = 9
    }
}
