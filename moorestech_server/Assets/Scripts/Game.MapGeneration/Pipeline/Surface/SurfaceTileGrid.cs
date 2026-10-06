using Game.MapGeneration.Surface;
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
            Geometry = SurfaceLattice.ForWorld(config, output.SceneOrigin);
            _tiles = new TerrainTileOutput[config.gridSizeZ, config.gridSizeX];

            // 配列順に依存せず、共有点の所有者をZ→X順に固定する
            // Fix shared vertex ownership in Z then X order, independently of array order
            var masks = new bool[config.gridSizeZ, config.gridSizeX][];
            if (output.Tiles.Count != config.gridSizeX * config.gridSizeZ || tileLandMasks.Length != output.Tiles.Count)
                throw SurfaceGenerationValidation.Failure(config, "all", "Incomplete surface tile set.");
            for (int i = 0; i < output.Tiles.Count; i++)
            {
                var tile = output.Tiles[i];
                if (tile.TileX < 0 || config.gridSizeX <= tile.TileX || tile.TileZ < 0 || config.gridSizeZ <= tile.TileZ ||
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

            #region Internal

            void ImportTile(int tileX, int tileZ, bool[] mask, bool[] landCells)
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
                    if (!SurfaceGenerationValidation.Finite(height) || height < 0f || 1f < height)
                        throw SurfaceGenerationValidation.Failure(Config, $"{tileX},{tileZ}", $"Invalid normalized height at {x},{z}.");

                    // 既読境界の不一致は隠さず失敗
                    // Fail on mismatched imported boundaries instead of hiding them
                    if ((0 < tileX && x == 0) || (0 < tileZ && z == 0))
                    {
                        int ownerX = SurfaceLattice.OwnerTile(globalX, stride);
                        int ownerZ = SurfaceLattice.OwnerTile(globalZ, stride);
                        float owner = _tiles[ownerZ, ownerX].Heights[(globalZ - ownerZ * stride) * res + globalX - ownerX * stride];
                        if (owner != height || landCells[global] != mask[local])
                            throw SurfaceGenerationValidation.Failure(Config, $"{tileX},{tileZ}", $"Shared vertex mismatch at {globalX},{globalZ}: owner={owner:R}, incoming={height:R}, ownerLand={landCells[global]}, incomingLand={mask[local]}.");
                    }
                    landCells[global] = mask[local];
                }
            }

            #endregion
        }

        public float GetHeight(int x, int z)
        {
            int stride = Config.Resolution - 1;
            int tileX = SurfaceLattice.OwnerTile(x, stride);
            int tileZ = SurfaceLattice.OwnerTile(z, stride);
            return _tiles[tileZ, tileX].Heights[(z - tileZ * stride) * Config.Resolution + x - tileX * stride] * Config.terrainHeight;
        }

        public void SetHeight(int x, int z, float meters)
        {
            int stride = Config.Resolution - 1;
            int firstX = SurfaceLattice.OwnerTile(x, stride);
            int firstZ = SurfaceLattice.OwnerTile(z, stride);
            int lastX = Mathf.Min(Config.gridSizeX - 1, x / stride);
            int lastZ = Mathf.Min(Config.gridSizeZ - 1, z / stride);

            // 共有頂点の所有4枚へ同じ値を書く
            // Write the same value to the up-to-four owner tiles
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

            // 内部境界を隣タイル解決で双線形補間
            // Resolve internal boundaries across tiles for bilinear interpolation
            return Mathf.Lerp(Mathf.Lerp(GetHeight(x, z), GetHeight(x + 1, z), point.x - x),
                Mathf.Lerp(GetHeight(x, z + 1), GetHeight(x + 1, z + 1), point.x - x), point.y - z);
        }

        public void ApplyLandFloor(SurfaceEnvelope envelope)
        {
            float floor = SurfaceQuantization.LandFloor(Config, envelope, "all");
            for (int z = 0; z < Geometry.Depth; z++)
            for (int x = 0; x < Geometry.Width; x++)
                if (Land.IsProtectedVertex(x, z)) SetHeight(x, z, Mathf.Max(GetHeight(x, z), floor));
        }
    }
}
