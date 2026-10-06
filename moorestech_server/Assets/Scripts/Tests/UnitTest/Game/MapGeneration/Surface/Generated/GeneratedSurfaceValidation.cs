using System.Collections.Generic;
using Core.Master;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Pipeline.Visual;
using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Surface.Generated
{
    internal static class GeneratedSurfaceValidation
    {
        internal static void Check(GeneratedSurfaceFixture fixture, bool repeatGeneration)
        {
            var first = fixture.BakePrebake();
            SurfaceGuaranteeMeasurement.Measure(fixture, fixture.Grid(first)).AssertValid();
            var hit = fixture.BakeReload(false);
            AssertSameHeights(first, hit);

            // cacheを除去して台帳再生成と最終表示の復元を強制する
            // Remove the cache to force ledger regeneration and final presentation reconstruction
            var miss = fixture.BakeReload(true);
            AssertSameHeights(first, miss);
            SurfaceGuaranteeMeasurement.Measure(fixture, fixture.Grid(miss)).AssertValid();
            if (repeatGeneration)
            {
                var repeated = MapGenerationPipeline.Generate(MasterHolder.GenerationMaster.SelectedGeneration, fixture.Run.Config);
                Assert.That(repeated.Ledger.ComputeDigest(), Is.EqualTo(fixture.Run.Ledger.ComputeDigest()));
                Assert.That(repeated.Output.SpawnPoint, Is.EqualTo(fixture.Run.Output.SpawnPoint));
                for (int index = 0; index < repeated.Output.Tiles.Count; index++)
                    CollectionAssert.AreEqual(fixture.Run.Output.Tiles[index].Heights, repeated.Output.Tiles[index].Heights);
            }
        }

        private static void AssertSameHeights(IReadOnlyList<TileVisualBakeResult> expected, IReadOnlyList<TileVisualBakeResult> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
            {
                // 全配列一致は共有境界と内部頂点の双方を含む
                // Entire array equality includes both shared boundaries and interior vertices
                Assert.That(actual[index].ScenePosition, Is.EqualTo(expected[index].ScenePosition));
                CollectionAssert.AreEqual(expected[index].DisplayHeights, actual[index].DisplayHeights);
            }
        }
    }
}
