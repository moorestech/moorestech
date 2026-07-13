namespace MapGenerator.Pipeline.Config
{
    // enum値 = スプラットマップ解像度(2^n)。ハイトマップは +1 (2^n+1)
    public enum TerrainResolutionPreset
    {
        _256  = 256,
        _512  = 512,
        _1024 = 1024,
        _2048 = 2048,
    }
}
