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
using Tests.UnitTest.Game.MapGeneration.Surface.Generated.Helpers;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    public sealed class GeneratedSurfaceFixture : IDisposable
    {
        public readonly GenerationRun Run;
        public readonly SurfaceTileGrid Original;
        public readonly WorldDataDirectory Saved;
        public readonly WorldDataDirectory Shared;
        public readonly GeneratedTerrainTransferMeta Meta;
        private readonly bool[][] _originalMasks;

        public GeneratedSurfaceFixture(int seed, int tiles, float width, float length)
        {
            // キャッシュIDを本番形式にし衝突を拒否
            // Use the production cache ID and reject collisions before generation
            var id = Guid.NewGuid().ToString("N").Substring(0, GameSystemPaths.WorldIdHexDigits);
            Saved = WorldDataDirectory.FromWorldRoot(Path.Combine(Path.GetTempPath(), "vtg-test-" + id));
            Shared = WorldDataDirectory.ForWorldCacheWithoutCreating(id);
            Assert.That(Directory.Exists(Saved.Root), Is.False, Saved.Root);
            Assert.That(Directory.Exists(Shared.Root), Is.False, Shared.Root);

            // 所有ディレクトリは失敗時も解放
            // Clean owned directories even when construction fails
            bool initialized = false;
            try
            {
                var inputs = new ProductionSurfaceInput();
                var generation = MasterHolder.GenerationMaster.SelectedGeneration;
                var config = MapGenerationPipeline.BuildConfig(generation, seed, inputs.ServerData, WorldGeneratorVersion.CurrentRevision);
                config.gridSizeX = tiles;
                config.gridSizeZ = tiles;
                // 探索の最終分類と同じ本番ピッチを維持する
                // Preserve the production pitch used by final spawn classification
                Assert.That(config.overrideResolution, Is.Zero);
                Assert.That(config.Resolution, Is.EqualTo(2049), "Pinned production master resolution");
                config.terrainWidth = width;
                config.terrainLength = length;

                // 探索後の原点を分類再構築にも使う
                // Reuse the settled origins for classification reconstruction
                var timer = SurfaceTestPhase.Start("generate");
                Run = MapGenerationPipeline.Generate(generation, config);
                SurfaceTestPhase.Finish("generate", timer);
                timer = SurfaceTestPhase.Start("original-grid-and-masks");
                Original = SurfaceGridBuilder.Build(Run.Config);
                _originalMasks = OriginalSurfaceMasks.Capture(Original, Run.Output);
                SurfaceTestPhase.Finish("original-grid-and-masks", timer);
                timer = SurfaceTestPhase.Start("write");
                TerrainFileWriter.Write(Saved, Run.Output);
                TerrainFileWriter.Write(Shared, Run.Output);
                SurfaceTestPhase.Finish("write", timer);

                // 保存メタのJSON往復後に表示を開く
                // Open presentation after round-tripping saved metadata through JSON
                timer = SurfaceTestPhase.Start("meta");
                Meta = SavedSurfaceMeta.WriteAndRead(Run, Saved, inputs.Fingerprint, id);
                Assert.That(Meta.GeneratedPayload.GeneratorVersion, Is.EqualTo("5.0.0"));
                SurfaceTestPhase.Finish("meta", timer);
                initialized = true;
            }
            finally
            {
                if (!initialized) Dispose();
            }
        }

        public List<TileVisualBakeResult> BakePrebake()
        {
            // サーバー先焼きと同じfactoryを使う
            // Use the same factory as server prebaking
            var timer = SurfaceTestPhase.Start("prebake");
            var factory = TileVisualBakerFactory.CreateForPrebake(Run.Config, Meta, Run.Ledger,
                MasterHolder.GenerationMaster.SelectedGeneration, Saved);
            var results = BakeAll(factory.Baker);
            SurfaceTestPhase.Finish("prebake", timer);
            return results;
        }

        public List<TileVisualBakeResult> BakeReload(bool removeVisualCache)
        {
            string phase = removeVisualCache ? "reload-miss" : "reload-hit";
            var timer = SurfaceTestPhase.Start(phase);
            if (removeVisualCache)
            {
                // 自分の一時world表示cacheを除去
                // Remove only this temporary world's visual cache
                foreach (var tile in Run.Output.Tiles)
                {
                    var path = Shared.TerrainVisualCacheFilePath(tile.TileX, tile.TileZ);
                    if (File.Exists(path)) File.Delete(path);
                    Assert.That(File.Exists(path), Is.False, path);
                }
            }
            var factory = TileVisualBakerFactory.CreateForClient(Run.Config, Meta,
                MasterHolder.GenerationMaster.SelectedGeneration);
            var results = BakeAll(factory.Baker);
            SurfaceTestPhase.Finish(phase, timer);
            return results;
        }

        public SurfaceTileGrid Grid(IReadOnlyList<TileVisualBakeResult> baked)
        {
            var output = new MapGenerationOutput
            {
                Resolution = Run.Output.Resolution,
                NoiseOrigin = Run.Output.NoiseOrigin,
                SceneOrigin = Run.Output.SceneOrigin,
            };
            int resolution = Run.Output.Resolution;
            for (int index = 0; index < baked.Count; index++)
            {
                var tile = Run.Output.Tiles[index];
                var values = new float[resolution * resolution];

                // 最終表示配列を共有格子へ写し不一致検出
                // Copy final display arrays to a shared lattice to detect seam mismatches
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    values[z * resolution + x] = SurfaceQuantization.StoredNormalized(baked[index].DisplayHeights[z, x]);
                }
                output.Tiles.Add(new TerrainTileOutput { TileX = tile.TileX, TileZ = tile.TileZ, Heights = values });
            }
            return new SurfaceTileGrid(output, _originalMasks, Run.Config);
        }

        private List<TileVisualBakeResult> BakeAll(TileVisualBaker baker)
        {
            var results = new List<TileVisualBakeResult>(Run.Output.Tiles.Count);
            foreach (var tile in Run.Output.Tiles) results.Add(baker.Bake(tile.TileX, tile.TileZ));
            return results;
        }

        public void Dispose()
        {
            // GUID隔離した自分の保存だけ片付ける
            // Clean up only the save and cache isolated by this GUID
            try
            {
                if (Directory.Exists(Saved.Root)) Directory.Delete(Saved.Root, true);
            }
            finally
            {
                if (Directory.Exists(Shared.Root)) Directory.Delete(Shared.Root, true);
            }
        }
    }
}
