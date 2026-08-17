using Client.Game.InGame.Environment.Terrain.Build.Placement;
using Client.Tests.UnitTest.Terrain;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Stages;
using NUnit.Framework;

namespace Client.Tests.UnitTest.Terrain.Classification
{
    /// <summary>
    ///     パディング窓が実際に境界帯の分類結果を動かしていることを検証する。
    ///     内陸一致だけを見るTerrainClassificationContextTestの受け入れ基準は、production から
    ///     PaddedWindowStage.Run を外してClassificationStage直叩きへ戻しても通ってしまい判別力を持たない。
    ///     このテストは境界帯で必ず食い違うことを固定し、その退行を検知する
    ///     Verifies the padded window actually changes classification results in the boundary band.
    ///     TerrainClassificationContextTest's interior-only acceptance check still passes even if production
    ///     reverts PaddedWindowStage.Run to a direct ClassificationStage call, so it has no discriminating power.
    ///     This test pins a required mismatch in the boundary band to catch that regression
    /// </summary>
    public class TerrainClassificationPaddingBoundaryTest
    {
        [Test]
        public void パディング窓は境界帯で等倍窓と食い違う()
        {
            var config = TerrainClassificationContextTest.BuildConfig();
            var biomeTypes = ClassificationStage.GetEnabledBiomeTypes(config);
            var reference = TerrainClassificationContextTest.RunUnpadded(config, biomeTypes);
            using var context = new TerrainClassificationContext(config, biomeTypes);
            context.Initialize();

            // 境界帯では padded と unpadded が必ず食い違う。一致したらパディングが効いていない
            // In the boundary band padded must differ from unpadded; a match means padding did nothing
            Assert.Less(0, CountMaskMismatchesInEdgeBand(reference.WinnerMasks, context.WinnerMasks),
                "境界帯でpaddedとunpaddedが一致した。PaddedWindowStageが効いていない");
        }

        // 内陸(Margin)の外側、窓端の影響が届く外周帯だけを走査する
        // Scans only the outer band beyond the interior (Margin) that the window edge reaches
        // TerrainClassificationContextTest.CountMaskMismatchesが見る内陸の裏返し
        // The mirror image of the interior that TerrainClassificationContextTest.CountMaskMismatches scans
        private static int CountMaskMismatchesInEdgeBand(bool[][,] expected, bool[][,] actual)
        {
            var resolution = TerrainClassificationContextTest.Resolution;
            var margin = TerrainClassificationContextTest.Margin;
            var mismatches = 0;
            for (var biomeIndex = 0; biomeIndex < expected.Length; biomeIndex++)
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var inEdgeBand = z < margin || resolution - margin <= z || x < margin || resolution - margin <= x;
                if (inEdgeBand && expected[biomeIndex][z, x] != actual[biomeIndex][z, x])
                    mismatches++;
            }

            return mismatches;
        }
    }
}
