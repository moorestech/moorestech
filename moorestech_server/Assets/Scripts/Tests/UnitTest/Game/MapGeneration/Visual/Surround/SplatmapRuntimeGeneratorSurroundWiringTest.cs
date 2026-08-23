using Game.MapGeneration.Pipeline.Visual.Surround;
using NUnit.Framework;
using static Tests.UnitTest.Game.MapGeneration.Visual.Surround.SurroundWiringTestScene;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Surround
{
    /// <summary>
    ///     SplatmapRuntimeGenerator.Generate から岩周辺の裸地までの結線を検証する。
    ///     painter単体テストはApplyを直接叩くため、その手前のMaxReach→SliceWithHalo→Splitを一度も通らない。
    ///     haloを0にする・切り出し済みの岩を渡す・タイル原点をVector3.zeroにする、のどれをやっても
    ///     タイル境界の外の岩が切り出しから落ちるので、ここが唯一その取り違えを捕まえる層になる
    ///     Exercises the wiring from SplatmapRuntimeGenerator.Generate down to the bare ground around rocks.
    ///     The painter unit tests call Apply directly and never run the MaxReach, SliceWithHalo and Split steps in front
    ///     of it; zeroing the halo, passing already-sliced rocks, or using Vector3.zero as the tile origin each drop the
    ///     rock outside the tile edge, and only this layer catches that
    /// </summary>
    public class SplatmapRuntimeGeneratorSurroundWiringTest
    {
        [SetUp]
        public void SetUp()
        {
            LoadMasterData();
        }

        [Test]
        public void ARockJustOutsideTheTileEdgeStillPaintsTheSeamPixel()
        {
            // 境界の5m外の岩。フットプリントの縁が境界画素に触るのでコア帯として強く寄る
            // A rock 5m past the seam, whose footprint edge touches the seam pixel and pulls it hard as a core-band pixel
            var alphamap = Generate(CreateStone(-5f, SeamLocalZ));

            Assert.That(alphamap[SeamPixelZ, SeamPixelX, MudLayerIndex], Is.GreaterThan(0.3f), "境界画素は隣タイルの岩で裸地へ寄る");

            // 岩から50m離れた隅は遷移帯の外。Z方向のローカル化が狂うとこちらが塗られる
            // The corner 50m from the rock sits outside the transition band; a broken Z rebasing would paint it instead
            Assert.That(alphamap[0, 0, MudLayerIndex], Is.EqualTo(0f), "遷移帯の外の隅は元のまま");
        }

        [Test]
        public void ARockBeyondMaxReachNeverEntersTheTile()
        {
            // MaxReachの外は切り出しで落ちる。裸地列が全画素で0のままなら、この経路が岩を持ち込んでいない
            // Past MaxReach the slice drops it; an all-zero bare-ground column proves this path brought no rock in
            var alphamap = Generate(CreateStone(-(ExpectedMaxReach + 5f), SeamLocalZ));

            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
                Assert.That(alphamap[z, x, MudLayerIndex], Is.EqualTo(0f), $"z={z} x={x}");
        }

        [Test]
        public void ARockJustInsideMaxReachStillReachesTheTile()
        {
            // 上の棄却テストと対で到達距離を挟み込む。到達距離が縮むとこちらが落ち、伸びると上が塗られる
            // Paired with the rejection test above this brackets the reach: a shrunken reach drops this rock and a grown one paints that one
            var alphamap = Generate(CreateStone(-(ExpectedMaxReach - 5f), SeamLocalZ));

            Assert.That(alphamap[SeamPixelZ, SeamPixelX, MudLayerIndex], Is.GreaterThan(0f), "到達距離の内側の岩は切り出しに残る");
        }
    }
}
