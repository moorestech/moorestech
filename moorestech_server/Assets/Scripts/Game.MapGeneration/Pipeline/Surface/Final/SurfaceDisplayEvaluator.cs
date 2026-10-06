using Game.MapGeneration.Surface;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Visual.Placement;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Surface
{
    internal static class SurfaceDisplayEvaluator
    {
        internal static SurfaceTileGrid Build(SurfaceTileGrid source, PlacementLedger ledger, bool projectFinal, SurfaceEnvelope envelope)
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

            var heightSource = new GeneratedSurfaceDisplayHeightSource(source);
            var boundaries = new SurfaceDisplayBoundaryOwner(gridConfig, heightSource);

            // 保存r16読戻しと同入力を木加工へ
            // Feed tree processing the same input as the r16 reload
            foreach (var tile in source.Output.Tiles)
            {
                var pre = heightSource.Load(tile.TileX, tile.TileZ);
                var mask = new bool[resolution * resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                    mask[z * resolution + x] = source.Land.IsLandVertex(tile.TileX * stride + x, tile.TileZ * stride + z);
                var tileConfig = gridConfig.CreateTileConfig(tile.TileX, tile.TileZ);
                var scene = source.Config.TileScenePosition(tile.TileX, tile.TileZ);
                var position = new Vector3(scene.x, 0f, scene.y);
                var post = TreePerturbationApplier.Apply(pre, tileConfig, position, ledger.Placements);
                if (projectFinal)
                    post = FinalSurfaceProjector.Apply(post, tileConfig, position, source.Land, ledger.GroundingPads, envelope);

                boundaries.CopyTo(post, position, ledger, source.Land, envelope, projectFinal);

                // 返却場は独立コピーで、保存用pre-treeへ書き戻さない
                // Evaluate Unity storage in independent fields without modifying saved pre-tree heights
                var heights = new float[resolution * resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    heights[z * resolution + x] = SurfaceQuantization.StoredNormalized(SurfaceQuantization.RoundTripR16(post[z, x]));
                }
                masks[output.Tiles.Count] = mask;
                output.Tiles.Add(new TerrainTileOutput { TileX = tile.TileX, TileZ = tile.TileZ, Heights = heights });
            }
            return new SurfaceTileGrid(output, masks, source.Config);
        }
    }
}
