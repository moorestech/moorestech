using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Visual;
using NUnit.Framework;
using Tests.UnitTest.Game.MapGeneration.Surface.Generated.Helpers;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    internal static class GeneratedSurfaceValidation
    {
        internal static void Check(GeneratedSurfaceFixture fixture, bool repeatGeneration)
        {
            CheckVisualReloads(fixture);
            if (repeatGeneration)
            {
                // 表示検証の配列を次の生成まで保持しない
                // End display validation array lifetimes before the next generation
                var timer = SurfaceTestPhase.Start("repeat-generate");
                var repeated = MapGenerationPipeline.Generate(MasterHolder.GenerationMaster.SelectedGeneration, fixture.Run.Config);
                SurfaceTestPhase.Finish("repeat-generate", timer);
                timer = SurfaceTestPhase.Start("compare-repeat");
                Assert.That(repeated.Ledger.ComputeDigest(), Is.EqualTo(fixture.Run.Ledger.ComputeDigest()));
                Assert.That(repeated.Output.SpawnPoint, Is.EqualTo(fixture.Run.Output.SpawnPoint));
                Assert.That(repeated.Output.Tiles.Count, Is.EqualTo(fixture.Run.Output.Tiles.Count));
                for (int index = 0; index < repeated.Output.Tiles.Count; index++)
                    SurfaceHeightAssert.AreEqual(fixture.Run.Output.Tiles[index].Heights,
                        repeated.Output.Tiles[index].Heights, $"repeat tile={index}");
                SurfaceTestPhase.Finish("compare-repeat", timer);
            }
        }

        private static void CheckVisualReloads(GeneratedSurfaceFixture fixture)
        {
            var first = fixture.BakePrebake();
            Measure(fixture, first, "measure-prebake");
            CheckReload(fixture, first, false);
            CheckReload(fixture, first, true);
        }

        private static void CheckReload(GeneratedSurfaceFixture fixture, IReadOnlyList<TileVisualBakeResult> first, bool removeCache)
        {
            // hit配列をmiss生成前に解放可能にし、両経路の全頂点比較を保つ
            // Allow hit arrays to be collected before miss generation while comparing every vertex on both paths
            var reloaded = fixture.BakeReload(removeCache);
            string phase = removeCache ? "compare-reload-miss" : "compare-reload-hit";
            var timer = SurfaceTestPhase.Start(phase);
            AssertSameHeights(first, reloaded);
            SurfaceTestPhase.Finish(phase, timer);
            if (removeCache) Measure(fixture, reloaded, "measure-reload-miss");
        }

        private static void Measure(GeneratedSurfaceFixture fixture, IReadOnlyList<TileVisualBakeResult> baked, string phase)
        {
            var timer = SurfaceTestPhase.Start(phase);
            SurfaceGuaranteeMeasurement.Measure(fixture, fixture.Grid(baked)).AssertValid();
            SurfaceTestPhase.Finish(phase, timer);
        }

        private static void AssertSameHeights(IReadOnlyList<TileVisualBakeResult> expected, IReadOnlyList<TileVisualBakeResult> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
            {
                // 全配列一致は共有境界と内部頂点の双方を含む
                // Entire array equality includes both shared boundaries and interior vertices
                Assert.That(actual[index].ScenePosition, Is.EqualTo(expected[index].ScenePosition));
                SurfaceHeightAssert.AreEqual(expected[index].DisplayHeights, actual[index].DisplayHeights, $"display tile={index}");
            }
        }
    }
}
