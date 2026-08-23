using UnityEngine;

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

        // detailの密度マップ解像度。heightmapとは独立に決まるので、寸法として別に運ぶ
        // The detail density maps' resolution; settled independently of the heightmap, so it travels as its own dimension
        public readonly int DetailResolution;

        public readonly float SeaLevel;
        public readonly float ShoreMinHeight;
        public readonly int Seed;
        public readonly float SpawnWorldX;
        public readonly float SpawnWorldZ;

        // 格子全体の原点と広がり。テクスチャノイズの UV をタイル幅ではなく格子全体で正規化するために持つ。
        // タイル位置はここへは残さない（格子原点の算出に使うだけで、乱数種の出所は TilePlacementContext）。
        // The whole grid's origin and extent, held so texture-noise UV normalizes over the grid rather than one tile.
        // The tile slot itself is not kept here: it only derives the grid origin, and the random seeds take theirs from TilePlacementContext.
        public readonly float GridOriginX;
        public readonly float GridOriginZ;
        public readonly float GridWidth;
        public readonly float GridLength;

        public TerrainDimensions(
            float terrainWidth, float terrainLength, float terrainHeight,
            float worldOffsetX, float worldOffsetZ,
            int resolution, int detailResolution, float seaLevel, float shoreMinHeight, int seed,
            float spawnWorldX, float spawnWorldZ,
            int tileIndexX, int tileIndexZ, int gridSizeX, int gridSizeZ)
        {
            TerrainWidth = terrainWidth;
            TerrainLength = terrainLength;
            TerrainHeight = terrainHeight;
            WorldOffsetX = worldOffsetX;
            WorldOffsetZ = worldOffsetZ;
            Resolution = resolution;
            DetailResolution = detailResolution;
            SeaLevel = seaLevel;
            ShoreMinHeight = shoreMinHeight;
            Seed = seed;
            SpawnWorldX = spawnWorldX;
            SpawnWorldZ = spawnWorldZ;

            // worldOffset はこのタイルの原点なので、タイル index ぶん戻すと格子の原点になる。
            // worldOffset is this tile's origin, so stepping back by the tile index lands on the grid's origin.
            GridOriginX = worldOffsetX - tileIndexX * terrainWidth;
            GridOriginZ = worldOffsetZ - tileIndexZ * terrainLength;
            GridWidth = gridSizeX * terrainWidth;
            GridLength = gridSizeZ * terrainLength;
        }

        // タイルローカル座標をワールド座標へ直しスポーンXZとの距離を返す（鉱脈・散布・クラスタで共通の基準）。
        // Converts tile-local coordinates to world space and returns the XZ distance to spawn (shared basis for veins, scatter, and clusters).
        public float DistanceFromSpawnXz(float localX, float localZ)
        {
            float dx = localX + WorldOffsetX - SpawnWorldX;
            float dz = localZ + WorldOffsetZ - SpawnWorldZ;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // このタイル矩形がスポーン地点から取りうる距離の最小・最大（リングが掛かるか判定するため）。
        // The minimum and maximum spawn distance this tile's rectangle can span, used to test whether a ring reaches it.
        public void SpawnDistanceRangeXz(out float nearestDistance, out float farthestDistance)
        {
            AxisRange(WorldOffsetX - SpawnWorldX, WorldOffsetX + TerrainWidth - SpawnWorldX, out var nearX, out var farX);
            AxisRange(WorldOffsetZ - SpawnWorldZ, WorldOffsetZ + TerrainLength - SpawnWorldZ, out var nearZ, out var farZ);
            nearestDistance = Mathf.Sqrt(nearX * nearX + nearZ * nearZ);
            farthestDistance = Mathf.Sqrt(farX * farX + farZ * farZ);

            #region Internal

            // スポーンを跨ぐ軸は最短0。跨がなければ近い端が最短で、遠い端が最長。
            // An axis straddling spawn has a zero minimum; otherwise the nearer edge is the minimum and the farther edge the maximum.
            void AxisRange(float low, float high, out float nearest, out float farthest)
            {
                nearest = low <= 0f && 0f <= high ? 0f : Mathf.Min(Mathf.Abs(low), Mathf.Abs(high));
                farthest = Mathf.Max(Mathf.Abs(low), Mathf.Abs(high));
            }

            #endregion
        }

        // TerrainGenerationConfig + 共通 waterMargin + タイル位置からファクトリ生成する。
        // Factory from TerrainGenerationConfig, the common waterMargin, and the tile's slot in the grid.
        public static TerrainDimensions From(
            TerrainGenerationConfig config, float waterMargin, int tileIndexX, int tileIndexZ)
        {
            return new TerrainDimensions(
                config.terrainWidth, config.terrainLength, config.terrainHeight,
                config.worldOffsetX, config.worldOffsetZ,
                config.Resolution, config.DetailResolution, config.seaLevel,
                config.seaLevel + waterMargin, config.seed,
                config.spawnWorldPosition.x, config.spawnWorldPosition.y,
                tileIndexX, tileIndexZ, config.gridSizeX, config.gridSizeZ);
        }
    }
}
