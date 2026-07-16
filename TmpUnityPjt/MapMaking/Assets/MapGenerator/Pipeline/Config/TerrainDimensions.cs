namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// ジェネレーターに渡す地形寸法の値型。
    /// TerrainGenerationConfig全体を渡さず、生成器が必要な寸法だけを切り出す。
    /// 生成器がConfigの他バイオーム設定にアクセスすることを構造的に防ぐ。
    /// </summary>
    public readonly struct TerrainDimensions
    {
        public readonly float TerrainWidth;
        public readonly float TerrainLength;
        public readonly float TerrainHeight;
        public readonly float WorldOffsetX;
        public readonly float WorldOffsetZ;
        public readonly int Resolution;
        public readonly float SeaLevel;
        // seaLevel + 共通waterMargin。海面近くの配置排除に使う
        public readonly float ShoreMinHeight;
        public readonly int Seed;
        // 鉱石距離バンドの中心となるスポーン地点（ワールド座標 m）
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

        // TerrainGenerationConfig + 共通waterMargin からファクトリ生成
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
