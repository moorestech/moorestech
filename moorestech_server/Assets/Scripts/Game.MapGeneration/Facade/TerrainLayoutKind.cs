namespace Game.MapGeneration.Facade
{
    // ワールドの地形をどちらの形で描くか。分岐はこの1本だけが持ち、TerrainTransferMeta.IsTemplate等を上位に漏らさない
    // How to render the world's terrain; this single discriminator, never TerrainTransferMeta.IsTemplate etc., is what upper layers branch on
    public enum TerrainLayoutKind
    {
        TerrainAsset,
        TileMaps,
    }
}
