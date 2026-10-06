using Game.MapGeneration.Facade.Surface;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    public sealed class SurfaceTileGrid
    {
        public readonly MapGenerationOutput Output;
        public readonly LandCellField Land;
        public readonly TerrainGenerationConfig Config;
        public readonly SurfaceLattice Geometry;
        private readonly TerrainTileOutput[,] _tiles;

        public SurfaceTileGrid(MapGenerationOutput output, bool[][] tileLandMasks, TerrainGenerationConfig config)
        {
            Output = output;
            Config = config;
            int stride = config.Resolution - 1;
            Geometry = new SurfaceLattice(output.SceneOrigin,
                new Vector2(config.terrainWidth / stride, config.terrainLength / stride),
                config.gridSizeX * stride + 1, config.gridSizeZ * stride + 1);
            _tiles = new TerrainTileOutput[config.gridSizeZ, config.gridSizeX];

            // 配列順に依存せず、共有点の所有者をZ→X順に固定する
            // Fix shared vertex ownership in Z then X order, independently of array order
            var masks = new bool[config.gridSizeZ, config.gridSizeX][];
            if (output.Tiles.Count != config.gridSizeX * config.gridSizeZ || tileLandMasks.Length != output.Tiles.Count)
                throw SurfaceGenerationValidation.Failure(config, "all", "Incomplete surface tile set.");
            for (int i = 0; i < output.Tiles.Count; i++)
            {
                var tile = output.Tiles[i];
                if (tile.TileX < 0 || tile.TileX >= config.gridSizeX || tile.TileZ < 0 || tile.TileZ >= config.gridSizeZ ||
                    _tiles[tile.TileZ, tile.TileX] != null || tile.Heights.Length != config.Resolution * config.Resolution ||
                    tileLandMasks[i].Length != tile.Heights.Length)
                    throw SurfaceGenerationValidation.Failure(config, $"{tile.TileX},{tile.TileZ}", "Invalid surface tile dimensions or duplicate tile.");
                _tiles[tile.TileZ, tile.TileX] = tile;
                masks[tile.TileZ, tile.TileX] = tileLandMasks[i];
            }

            var land = new bool[Geometry.Width * Geometry.Depth];
            for (int z = 0; z < config.gridSizeZ; z++)
            for (int x = 0; x < config.gridSizeX; x++)
                ImportTile(x, z, masks[z, x], land);
            Land = new LandCellField(Geometry, land);
        }

        public float GetHeight(int x, int z)
        {
            int stride = Config.Resolution - 1;
            int tileX = Mathf.Max(0, (x - 1) / stride);
            int tileZ = Mathf.Max(0, (z - 1) / stride);
            return _tiles[tileZ, tileX].Heights[(z - tileZ * stride) * Config.Resolution + x - tileX * stride] * Config.terrainHeight;
        }

        public void SetHeight(int x, int z, float meters)
        {
            int stride = Config.Resolution - 1;
            int firstX = Mathf.Max(0, (x - 1) / stride);
            int firstZ = Mathf.Max(0, (z - 1) / stride);
            int lastX = Mathf.Min(Config.gridSizeX - 1, x / stride);
            int lastZ = Mathf.Min(Config.gridSizeZ - 1, z / stride);

            // 共有頂点を所有する最大4枚へ同じ正規化値を書き込む
            // Write the same normalized value to up to four tiles sharing this vertex
            for (int tileZ = firstZ; tileZ <= lastZ; tileZ++)
            for (int tileX = firstX; tileX <= lastX; tileX++)
                _tiles[tileZ, tileX].Heights[(z - tileZ * stride) * Config.Resolution + x - tileX * stride] = meters / Config.terrainHeight;
        }

        public float SampleHeight(Vector2 scene)
        {
            if (!Geometry.Contains(new Rect(scene, Vector2.zero)))
                throw SurfaceGenerationValidation.Failure(Config, "sample", $"Sample outside world at {scene}.");
            var point = Geometry.GridPosition(scene);
            int x = Mathf.Min(Mathf.FloorToInt(point.x), Geometry.Width - 2);
            int z = Mathf.Min(Mathf.FloorToInt(point.y), Geometry.Depth - 2);

            // 内部境界を隣タイルへ解決して双線形補間する
            // Resolve internal boundaries across tiles before bilinear interpolation
            return Mathf.Lerp(Mathf.Lerp(GetHeight(x, z), GetHeight(x + 1, z), point.x - x),
                Mathf.Lerp(GetHeight(x, z + 1), GetHeight(x + 1, z + 1), point.x - x), point.y - z);
        }

        public void ApplyLandFloor(SurfaceEnvelope envelope)
        {
            float floor = SurfaceQuantization.LandFloor(Config.terrainHeight, envelope);
            for (int z = 0; z < Geometry.Depth; z++)
            for (int x = 0; x < Geometry.Width; x++)
                if (Land.IsProtectedVertex(x, z)) SetHeight(x, z, Mathf.Max(GetHeight(x, z), floor));
        }

        private void ImportTile(int tileX, int tileZ, bool[] mask, bool[] land)
        {
            int res = Config.Resolution;
            int stride = res - 1;
            var tile = _tiles[tileZ, tileX];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int local = z * res + x;
                int globalX = tileX * stride + x;
                int globalZ = tileZ * stride + z;
                int global = globalZ * Geometry.Width + globalX;
                float height = tile.Heights[local];
                if (!SurfaceGenerationValidation.Finite(height) || height < 0f || height > 1f)
                    throw SurfaceGenerationValidation.Failure(Config, $"{tileX},{tileZ}", $"Invalid normalized height at {x},{z}.");

                // 既に読んだ境界値の不一致は隠さず生成を失敗させる
                // Fail generation rather than hiding mismatched previously imported boundaries
                if ((tileX > 0 && x == 0) || (tileZ > 0 && z == 0))
                {
                    int ownerX = Mathf.Max(0, (globalX - 1) / stride);
                    int ownerZ = Mathf.Max(0, (globalZ - 1) / stride);
                    float owner = _tiles[ownerZ, ownerX].Heights[(globalZ - ownerZ * stride) * res + globalX - ownerX * stride];
                    if (owner != height || land[global] != mask[local])
                        throw SurfaceGenerationValidation.Failure(Config, $"{tileX},{tileZ}", $"Shared vertex mismatch at {globalX},{globalZ}.");
                }
                land[global] = mask[local];
            }
        }
    }
}
