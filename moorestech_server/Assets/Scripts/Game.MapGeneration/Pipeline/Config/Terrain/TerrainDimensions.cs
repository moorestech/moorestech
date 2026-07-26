namespace Game.MapGeneration.Pipeline.Config
{
    // ジェネレーターに渡す地形寸法の値型。Config 全体を渡さず必要な寸法だけ切り出す。
    // Value type of terrain dimensions handed to generators, isolating them from the full config.
    public readonly struct TerrainDimensions
    {
        public readonly float TerrainWidth;
        public readonly float TerrainLength;
        public readonly float TerrainHeight;
        public readonly float WorldOffsetX;
        public readonly float WorldOffsetZ;
        public readonly int Resolution;
        public readonly float SeaLevel;
        public readonly float ShoreMinHeight;
        public readonly int Seed;
        public readonly float SpawnWorldX;
        public readonly float SpawnWorldZ;

        public TerrainDimensions(
            float terrainWidth, float terrainLength, float terrainHeight,
            float worldOffsetX, float worldOffsetZ,
            int resolution, float seaLevel, float shoreMinHeight, int seed,
            float spawnWorldX, float spawnWorldZ)
        {
            TerrainWidth = terrainWidth;
            TerrainLength = terrainLength;
            TerrainHeight = terrainHeight;
            WorldOffsetX = worldOffsetX;
            WorldOffsetZ = worldOffsetZ;
            Resolution = resolution;
            SeaLevel = seaLevel;
            ShoreMinHeight = shoreMinHeight;
            Seed = seed;
            SpawnWorldX = spawnWorldX;
            SpawnWorldZ = spawnWorldZ;
        }

        // TerrainGenerationConfig + 共通 waterMargin からファクトリ生成する。
        // Factory from TerrainGenerationConfig plus the common waterMargin.
        public static TerrainDimensions From(TerrainGenerationConfig config, float waterMargin)
        {
            return new TerrainDimensions(
                config.terrainWidth, config.terrainLength, config.terrainHeight,
                config.worldOffsetX, config.worldOffsetZ,
                config.Resolution, config.seaLevel,
                config.seaLevel + waterMargin, config.seed,
                config.spawnWorldPosition.x, config.spawnWorldPosition.y);
        }
    }
}
