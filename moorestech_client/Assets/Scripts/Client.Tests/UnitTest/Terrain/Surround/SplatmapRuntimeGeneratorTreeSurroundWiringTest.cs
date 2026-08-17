using NUnit.Framework;
using static Client.Tests.UnitTest.Terrain.Surround.SurroundWiringTestScene;

namespace Client.Tests.UnitTest.Terrain.Surround
{
    /// <summary>
    ///     SplatmapRuntimeGenerator.Generate から木の根元の塗りまでの結線を検証する。
    ///     painter単体テストはApplyを直接叩くので、その手前のMaxReach→SliceWithHalo→Split と適用順を一度も通らない。
    ///     切り出しhaloを岩の20mへ縮める・木を岩側へ振り分ける・木を岩より先に塗る、のどれも単体テストは素通りする
    ///     Exercises the wiring from SplatmapRuntimeGenerator.Generate down to the painting under a tree's root.
    ///     The painter unit tests call Apply directly and never run the MaxReach, SliceWithHalo and Split steps in front of
    ///     it nor the ordering; shrinking the halo to the rocks' 20m, sorting a tree onto the rock side, or painting trees
    ///     before rocks each slip past them
    /// </summary>
    public class SplatmapRuntimeGeneratorTreeSurroundWiringTest
    {
        // 幅30mの根元が境界画素へ2画素ぶんの距離で届く位置。岩のMaxReach20mでは切り出しから落ちる
        // A 30m root reaches the seam pixel from two pixels away here, while the rocks' 20m MaxReach drops it from the slice
        private const float InsideRockReachLocalX = -24f;

        // 到達上限30mのすぐ内側。haloが1mでも狭まると落ちるので、部分的な狭まりもここで捕まる
        // Just inside the 30m limit, dropped by even a one-metre narrower halo, so a partial shrink is caught here too
        private const float AtTreeReachLocalX = -29f;

        [SetUp]
        public void SetUp()
        {
            LoadMasterData();
        }

        [Test]
        public void ATreeOutsideTheRocksReachStillPaintsTheSeamPixel()
        {
            var alphamap = Generate(CreateTree(InsideRockReachLocalX, SeamLocalZ));

            Assert.That(alphamap[SeamPixelZ, SeamPixelX, TreeRootLayerIndex], Is.GreaterThan(0.05f), "境界画素は隣タイルの木で根元色へ寄る");

            // 木から50m離れた隅は幅30mの外。Z方向のローカル化が狂うとこちらが塗られる
            // The corner 50m from the tree sits outside the 30m width; a broken Z rebasing would paint it instead
            Assert.That(alphamap[0, 0, TreeRootLayerIndex], Is.EqualTo(0f), "幅の外の隅は元のまま");
        }

        [Test]
        public void ATreeAtTheEdgeOfItsWidthStillTouchesTheSeamPixel()
        {
            // 到達上限ちょうどの内側と外側で対照を取る。haloが幅を下回るとこの1本だけが黙って消える
            // Straddling the limit: a halo below the width silently drops this one tree alone
            var insideAlphamap = Generate(CreateTree(AtTreeReachLocalX, SeamLocalZ));
            var outsideAlphamap = Generate(CreateTree(-(TreeSurroundWidth + 1f), SeamLocalZ));

            Assert.That(insideAlphamap[SeamPixelZ, SeamPixelX, TreeRootLayerIndex], Is.GreaterThan(0f));
            Assert.That(outsideAlphamap[SeamPixelZ, SeamPixelX, TreeRootLayerIndex], Is.EqualTo(0f));
        }

        [Test]
        public void TheHaloTheTestsAssumeMatchesTheConfiguredWidth()
        {
            // 上の2本は30mを境に内外を置いている。設定を変えたときにその前提だけが黙って崩れるのを防ぐ
            // The two tests above straddle 30m, and this keeps a config change from silently invalidating that premise
            Assert.That(TreeSurroundSpecies().MaxReach, Is.EqualTo(TreeSurroundWidth).Within(1e-4f));
        }

        [Test]
        public void TheTreeRootIsPaintedAfterTheRocksBareGroundOnTheSharedPixel()
        {
            // 同じ画素に岩と木を置く。根元の重み1は他レイヤーを0にするので、後から岩が塗れば裸地列が0でなくなる
            // A rock and a tree share one pixel; a root weight of 1 zeroes the other layers, so a rock painting afterwards would leave the bare-ground column above zero
            var alphamap = Generate(CreateStone(SeamLocalZ, SeamLocalZ), CreateTree(SeamLocalZ, SeamLocalZ));

            // ローカル50mは両軸ともSeamPixelZの索引へ落ちる
            // A local 50m lands on the SeamPixelZ index along both axes
            Assert.That(alphamap[SeamPixelZ, SeamPixelZ, TreeRootLayerIndex], Is.EqualTo(1f).Within(1e-4f));
            Assert.That(alphamap[SeamPixelZ, SeamPixelZ, MudLayerIndex], Is.EqualTo(0f));
        }

        [Test]
        public void ARockNeverPaintsTheTreeRootLayer()
        {
            // 岩のguidは樹種テーブルに載せてある(SurroundWiringTestConfig)ので、根元の列を守るのはSplitの振り分けだけ
            // The rock's guid sits in the species table (SurroundWiringTestConfig), so Split's sorting alone keeps the root column clean
            // outを取り違えると岩がそのまま根元色で塗られる
            // Swapping its out arguments paints the rock with the root colour outright
            var alphamap = Generate(CreateStone(SeamLocalZ, SeamLocalZ));

            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
                Assert.That(alphamap[z, x, TreeRootLayerIndex], Is.EqualTo(0f), $"z={z} x={x}");
        }
    }
}
