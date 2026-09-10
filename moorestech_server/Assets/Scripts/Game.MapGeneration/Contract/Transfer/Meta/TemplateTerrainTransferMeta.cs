namespace Game.MapGeneration.Transfer
{
    // オーサリング済み地形のワールド。地形バイナリも生成専用値も持たないので、それらを表すフィールド自体が存在しない
    // A world on an authored terrain; it owns neither terrain binaries nor generated-only values, so no field for them exists at all
    public sealed class TemplateTerrainTransferMeta : TerrainTransferMeta
    {
        public TemplateTerrainTransferMeta(string worldId, int worldSeed)
            : base(WorldMapMode.Template, worldId, worldSeed)
        {
        }
    }
}
