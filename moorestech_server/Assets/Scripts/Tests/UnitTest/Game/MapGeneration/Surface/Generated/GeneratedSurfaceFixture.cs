using System;
using System.Collections.Generic;
using System.IO;
using Core.Master;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Surface;
using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    public sealed class GeneratedSurfaceFixture : IDisposable
    {
        public readonly GenerationRun Run;
        public readonly SurfaceTileGrid Original;
        public readonly WorldDataDirectory Saved;
        public readonly WorldDataDirectory Shared;
        public readonly GeneratedTerrainTransferMeta Meta;

        public GeneratedSurfaceFixture(int seed, int tiles, float width, float length)
        {
            // キャッシュIDを本番形式に揃え、既存worldとの衝突を生成前に拒否する
            // Use the production cache ID format and reject existing worlds before generation
            var id = Guid.NewGuid().ToString("N").Substring(0, GameSystemPaths.WorldIdHexDigits);
            Saved = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "vtg-test-" + id));
            Shared = WorldDataDirectory.ForWorldCacheWithoutCreating(id);
            Assert.That(Directory.Exists(Saved.Root), Is.False, Saved.Root);
            Assert.That(Directory.Exists(Shared.Root), Is.False, Shared.Root);

            var inputs = new ProductionSurfaceInput();
            var generation = MasterHolder.GenerationMaster.SelectedGeneration;
            var config = MapGenerationPipeline.BuildConfig(generation, seed, inputs.ServerData);
            config.gridSizeX = tiles;
            config.gridSizeZ = tiles;
            // 探索の最終分類と同じ本番ピッチを維持する
            // Preserve the production pitch used by final spawn classification
            Assert.That(config.overrideResolution, Is.Zero);
            Assert.That(config.Resolution, Is.EqualTo(2049), "Pinned production master resolution");
            config.terrainWidth = width;
            config.terrainLength = length;

            // 本番配置・探索を通した後の原点を分類再構築にも使う
            // Use origins settled by production placement and search for classification reconstruction
            Run = MapGenerationPipeline.Generate(generation, config);
            Original = SurfaceGridBuilder.Build(Run.Config);
            TerrainFileWriter.Write(Saved, Run.Output);
            TerrainFileWriter.Write(Shared, Run.Output);

            // 保存メタのJSON往復後に表示を開く
            // Open presentation after round-tripping saved metadata through JSON
            Meta = SavedSurfaceMeta.WriteAndRead(Run, Saved, inputs.Fingerprint, id);
            Assert.That(Meta.GeneratedPayload.GeneratorVersion, Is.EqualTo("5.0.0"));
        }

        public List<TileVisualBakeResult> BakePrebake()
        {
            // サーバー先焼きと同じfactoryを使う
            // Use the same factory as server prebaking
            var factory = TileVisualBakerFactory.CreateForPrebake(Run.Config, Meta, Run.Ledger,
                MasterHolder.GenerationMaster.SelectedGeneration, Saved);
            return BakeAll(factory.Baker);
        }

        public List<TileVisualBakeResult> BakeReload(bool removeVisualCache)
        {
            if (removeVisualCache)
            {
                // 自分の一時worldの表示cacheだけを除去する
                // Remove only the visual cache owned by this temporary world
                foreach (var tile in Run.Output.Tiles)
                {
                    var path = Shared.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ);
                    if (File.Exists(path)) File.Delete(path);
                    Assert.That(File.Exists(path), Is.False, path);
                }
            }
            var factory = TileVisualBakerFactory.CreateForClient(Run.Config, Meta,
                MasterHolder.GenerationMaster.SelectedGeneration);
            return BakeAll(factory.Baker);
        }

        public SurfaceTileGrid Grid(IReadOnlyList<TileVisualBakeResult> baked)
        {
            var output = new MapGenerationOutput
            {
                Resolution = Run.Output.Resolution,
                NoiseOrigin = Run.Output.NoiseOrigin,
                SceneOrigin = Run.Output.SceneOrigin,
            };
            var masks = new bool[baked.Count][];
            int resolution = Run.Output.Resolution;
            int stride = resolution - 1;
            for (int index = 0; index < baked.Count; index++)
            {
                var tile = Run.Output.Tiles[index];
                var values = new float[resolution * resolution];
                var mask = new bool[values.Length];

                // 最終表示配列を共有格子へ写し境界不一致も検出する
                // Copy final display arrays into a shared lattice that also detects seam mismatches
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    values[z * resolution + x] = SurfaceQuantization.StoredNormalized(baked[index].DisplayHeights[z, x]);
                    mask[z * resolution + x] = Original.Land.IsLandVertex(tile.TileX * stride + x, tile.TileZ * stride + z);
                }
                masks[index] = mask;
                output.Tiles.Add(new TerrainTileOutput { TileX = tile.TileX, TileZ = tile.TileZ, Heights = values });
            }
            return new SurfaceTileGrid(output, masks, Run.Config);
        }

        private List<TileVisualBakeResult> BakeAll(TileVisualBaker baker)
        {
            var results = new List<TileVisualBakeResult>();
            foreach (var tile in Run.Output.Tiles) results.Add(baker.Bake(tile.TileX, tile.TileZ));
            return results;
        }

        public void Dispose()
        {
            // GUIDで隔離した自分の保存・cacheだけを片付ける
            // Clean up only the save and cache isolated by this fixture's GUID
            if (Directory.Exists(Saved.Root)) Directory.Delete(Saved.Root, true);
            if (Directory.Exists(Shared.Root)) Directory.Delete(Shared.Root, true);
        }
    }
}
