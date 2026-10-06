using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Surface;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated.Helpers
{
    internal static class OriginalSurfaceMasks
    {
        internal static bool[][] Capture(SurfaceTileGrid original, MapGenerationOutput output)
        {
            int resolution = output.Resolution;
            int stride = resolution - 1;
            var masks = new bool[output.Tiles.Count][];

            // 不変の分類を一度保存し全頂点検査へ
            // Capture immutable classification once for full vertex checks
            for (int index = 0; index < masks.Length; index++)
            {
                var tile = output.Tiles[index];
                var mask = new bool[resolution * resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                    mask[z * resolution + x] = original.Land.IsLandVertex(tile.TileX * stride + x, tile.TileZ * stride + z);
                masks[index] = mask;
            }
            return masks;
        }
    }
}
