using Game.MapGeneration.Cache;
using Game.Paths;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal sealed class StoredSurfaceDisplayHeightSource : ISurfaceDisplayHeightSource
    {
        private readonly WorldDataDirectory _directory;
        private readonly int _resolution;

        internal StoredSurfaceDisplayHeightSource(WorldDataDirectory directory, int resolution)
        {
            _directory = directory;
            _resolution = resolution;
        }

        public float[,] Load(int tileX, int tileZ)
        {
            return HeightFileLoader.LoadHeights(_directory, tileX, tileZ, _resolution);
        }
    }

    internal sealed class GeneratedSurfaceDisplayHeightSource : ISurfaceDisplayHeightSource
    {
        private readonly SurfaceTileGrid _grid;

        internal GeneratedSurfaceDisplayHeightSource(SurfaceTileGrid grid)
        {
            _grid = grid;
        }

        public float[,] Load(int tileX, int tileZ)
        {
            // 保存r16と同入力を所有タイルへ渡す
            // Feed the owner tile the same input as stored r16
            int resolution = _grid.Config.Resolution;
            foreach (var tile in _grid.Output.Tiles)
            {
                if (tile.TileX != tileX || tile.TileZ != tileZ) continue;
                var heights = new float[resolution, resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                    heights[z, x] = SurfaceQuantization.RoundTripR16(tile.Heights[z * resolution + x]);
                return heights;
            }
            throw SurfaceGenerationValidation.Failure(_grid.Config, $"{tileX},{tileZ}", "Missing display owner tile.");
        }
    }
}
