using NUnit.Framework;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Splat
{
    /// <summary>
    ///     デバッグ列が1本も確保されなかったときにオーバーレイが走らないことを固定する。実v8は
    ///     debugPlateauOverlay:true のまま列アドレスが空文字5本で、走らせるとPlateauDebugOverlayJobが
    ///     棄却側の枝へ落ちて台地の全画素を全消し＋Alpineベース色でベタ塗りする
    ///     Pins that the overlay does not run when no debug column was reserved; the real v8 keeps debugPlateauOverlay true
    ///     with five empty column addresses, and running it drops PlateauDebugOverlayJob into its rejected branch,
    ///     wiping every plateau pixel and flooding it with Alpine's base colour
    /// </summary>
    public class PlateauDebugOverlayGateTest
    {
        private const int AlphamapResolution = PlateauOverlayTestFixtures.AlphamapResolution;

        [Test]
        public void デバッグ列0本ならオーバーレイは台地を1画素も塗り替えない()
        {
            // 塗る側だけを止めた走行と、オーバーレイ自体を切った走行。全画素一致でなければ列0本でも塗ってしまっている
            // One run with only the painting suppressed and one with the overlay switched off; any disagreement means zero columns still painted
            var emptyColumns = PlateauOverlayTestFixtures.Generate(
                PlateauOverlayTestFixtures.CreateConfig(), PlateauOverlayTestFixtures.CreateLayerTable(), out var channels);

            var overlayDisabled = PlateauOverlayTestFixtures.CreateConfig();
            overlayDisabled.alpine.debugPlateauOverlay = false;
            var baseline = PlateauOverlayTestFixtures.Generate(
                overlayDisabled, PlateauOverlayTestFixtures.CreateLayerTable(), out _);

            // 台地候補が1つも無いと塗り潰しが起きようが無く、この比較は何も検出しない
            // With no plateau candidate there is nothing to flood and the comparison would detect nothing
            var candidatePixels = 0;
            foreach (var maskValue in channels.PlateauMask)
                if (0f < maskValue) candidatePixels++;

            Assert.Less(0, candidatePixels, "台地候補が無く、塗り潰しを検出できない");

            var changedPixels = 0;
            var layerCount = baseline.GetLength(2);
            for (var z = 0; z < AlphamapResolution; z++)
            for (var x = 0; x < AlphamapResolution; x++)
            for (var layer = 0; layer < layerCount; layer++)
                if (emptyColumns[z, x, layer] != baseline[z, x, layer]) changedPixels++;

            Assert.AreEqual(0, changedPixels, $"デバッグ列0本のまま{changedPixels}画素が塗り替えられている");
        }
    }
}
