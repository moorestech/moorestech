using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class SurfaceDisplayEvaluator
    {
        internal static SurfaceTileGrid Build(SurfaceTileGrid source, PlacementLedger ledger, bool projectFinal)
        {
            var output = new MapGenerationOutput
            {
                Resolution = source.Output.Resolution,
                NoiseOrigin = source.Output.NoiseOrigin,
                SceneOrigin = source.Output.SceneOrigin,
            };
            var masks = new bool[source.Output.Tiles.Count][];
            int resolution = source.Config.Resolution;
            int stride = resolution - 1;
            var gridConfig = source.Config.ShallowCopy();
            gridConfig.worldOffsetX = output.NoiseOrigin.x;
            gridConfig.worldOffsetZ = output.NoiseOrigin.y;

            // 保存r16の読み戻しと同じ入力を木加工へ渡す
            // Feed tree processing the same inputs as the saved r16 reload
            foreach (var tile in source.Output.Tiles)
            {
                var pre = new float[resolution, resolution];
                var mask = new bool[resolution * resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    pre[z, x] = Mathf.Clamp(Mathf.RoundToInt(tile.Heights[z * resolution + x] * ushort.MaxValue), 0, ushort.MaxValue) / (float)ushort.MaxValue;
                    mask[z * resolution + x] = source.Land.IsLandVertex(tile.TileX * stride + x, tile.TileZ * stride + z);
                }
                var tileConfig = gridConfig.CreateTileConfig(tile.TileX, tile.TileZ);
                var scene = source.Config.TileScenePosition(tile.TileX, tile.TileZ);
                var position = new Vector3(scene.x, 0f, scene.y);
                var post = TreePerturbationApplier.Apply(pre, tileConfig, position, ledger.Placements);
                if (projectFinal)
                    post = FinalSurfaceProjector.Apply(post, tileConfig, position, source.Land, ledger.GroundingPads);

                // 返却場は独立コピーで、保存用pre-treeへ書き戻さない
                // Return independent fields without writing back into saved pre-tree heights
                var heights = new float[resolution * resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++) heights[z * resolution + x] = Mathf.Clamp(Mathf.RoundToInt(post[z, x] * ushort.MaxValue), 0, ushort.MaxValue) / (float)ushort.MaxValue;
                masks[output.Tiles.Count] = mask;
                output.Tiles.Add(new TerrainTileOutput { TileX = tile.TileX, TileZ = tile.TileZ, Heights = heights });
            }
            return new SurfaceTileGrid(output, masks, source.Config);
        }
    }
}
