using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Surface;

namespace Tests.UnitTest.Game.MapGeneration.Surface
{
    internal static class SurfaceGridFixture
    {
        internal static SurfaceTileGrid Create(int side, int resolution, float width, float length, bool land)
        {
            var config = new TerrainGenerationConfig
            {
                gridSizeX = side, gridSizeZ = side, overrideResolution = resolution,
                terrainWidth = width, terrainLength = length, terrainHeight = 600f,
            };
            var output = new MapGenerationOutput { Resolution = resolution, SceneOrigin = config.TileScenePosition(0, 0) };

            // 意図的に低い高さから本番の格子とfloorを検証する
            // Exercise the production lattice and floor with intentionally low heights
            for (int z = 0; z < side; z++)
            for (int x = 0; x < side; x++)
                output.Tiles.Add(new TerrainTileOutput { TileX = x, TileZ = z, Heights = new float[resolution * resolution] });
            return new SurfaceTileGrid(output, Masks(side * side, resolution * resolution, land), config);
        }

        internal static bool[][] Masks(int count, int length, bool land)
        {
            var masks = new bool[count][];
            for (int tile = 0; tile < count; tile++)
            {
                masks[tile] = new bool[length];
                for (int i = 0; i < length; i++) masks[tile][i] = land;
            }
            return masks;
        }
    }
}
