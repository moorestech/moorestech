using NUnit.Framework;

namespace Client.Tests.UnitTest.Terrain.Splat
{
    /// <summary>
    ///     SplatmapRuntimeGenerator.Generate から台地デバッグオーバーレイまでの結線を検証する。
    ///     台地の判定はパディング窓で分類したときにしか出ないので、分類チャネルのクロップまで通しで効いていないと塗られない
    ///     Exercises the wiring from SplatmapRuntimeGenerator.Generate down to the plateau debug overlay; the plateau
    ///     verdict exists only in the padded-window classification, so nothing is painted unless its crop works end to end
    /// </summary>
    public class PlateauDebugOverlayWiringTest
    {
        private const int AlphamapResolution = PlateauOverlayTestFixtures.AlphamapResolution;
        private const string DebugLayerAddress = PlateauOverlayTestFixtures.DebugLayerAddress;

        [Test]
        public void PaintsAcceptedPlateausOnTheDebugColumn()
        {
            // 1画素も塗られないならオーバーレイが走っていないか、台地チャネルが空のまま渡っている
            // Not one painted pixel means either the overlay never ran or the plateau channels arrived empty
            var layerTable = PlateauOverlayTestFixtures.CreateLayerTable(DebugLayerAddress);
            var alphamap = PlateauOverlayTestFixtures.Generate(
                PlateauOverlayTestFixtures.CreateConfig(), layerTable, out _);

            var paintedPixels = 0;
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
                if (0.5f < alphamap[z, x, layerTable.DebugLayerStart]) paintedPixels++;

            Assert.Less(0, paintedPixels, "デバッグ列が塗られていない");
        }

        [Test]
        public void LeavesTheSplatmapAloneWhereNoPlateauWasAccepted()
        {
            // オーバーレイを走らせた走行と走らせない走行を突き合わせる
            // Compares a run with the overlay against one without it
            // 受理領域とも棄却候補とも無関係な画素が動けば、オーバーレイが台地の外へ漏れている
            // A moved pixel belonging to neither an accepted region nor a rejected candidate means a leak outside the plateaus
            var withDebug = PlateauOverlayTestFixtures.Generate(
                PlateauOverlayTestFixtures.CreateConfig(),
                PlateauOverlayTestFixtures.CreateLayerTable(DebugLayerAddress), out var channels);
            var withoutDebug = PlateauOverlayTestFixtures.Generate(
                PlateauOverlayTestFixtures.CreateConfig(), PlateauOverlayTestFixtures.CreateLayerTable(), out _);

            var debugColumn = PlateauOverlayTestFixtures.CreateLayerTable(DebugLayerAddress).DebugLayerStart;
            var comparedLayers = withoutDebug.GetLength(2);
            var changedPixels = 0;
            var strayDebugPixels = 0;
            var rejectedCandidatePixels = 0;
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            {
                var source = PlateauOverlayTestFixtures.SourcePixelIndex(z, x);
                if (0 < channels.RegionLabels[source]) continue;
                if (0f < withDebug[z, x, debugColumn]) strayDebugPixels++;

                // 棄却候補はオーバーレイがベース色で塗り直す仕様。走らせない走行と食い違って当然なので比較から外す
                // A rejected candidate is repainted with the base colour by design, so it is expected to differ from the run without the overlay
                if (0f < channels.PlateauMask[source])
                {
                    rejectedCandidatePixels++;
                    continue;
                }

                for (var layer = 0; layer < comparedLayers; layer++)
                    if (withDebug[z, x, layer] != withoutDebug[z, x, layer]) changedPixels++;
            }

            // 棄却候補が1つも無いと「候補を全部塗る」壊し方が素通りする。緩めた閾値でも棄却は必ず出る
            // With no rejected candidate the "paint every candidate" break would slip through; the loosened thresholds always leave some
            Assert.Less(0, rejectedCandidatePixels, "棄却された台地候補が無く、塗り過ぎを検出できない");

            // デバッグ列は受理領域だけの持ち物。棄却候補や平地に薄く乗るだけでも受理と棄却の区別を失っている
            // The debug column belongs to accepted regions alone; even a faint trace on a rejected candidate or flat ground means the split was lost
            Assert.AreEqual(0, strayDebugPixels, $"受理されていない{strayDebugPixels}画素にデバッグ列が乗っている");
            Assert.AreEqual(0, changedPixels, "台地でも候補でもない画素の合成が動いている");
        }
    }
}
