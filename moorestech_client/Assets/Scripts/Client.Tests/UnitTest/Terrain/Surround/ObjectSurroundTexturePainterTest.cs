using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;
using static Client.Tests.UnitTest.Terrain.Surround.SurroundTestFixtures;

namespace Client.Tests.UnitTest.Terrain.Surround
{
    /// <summary>
    ///     岩の周りのalphamapが裸地レイヤーへ寄るかを検証する。重み合計・遷移帯の届く範囲・クラスタと単体の経路差・
    ///     バイオーム別設定の引き分けを見て、移植した式のどれが欠けても落ちるようにしている
    ///     Checks that the alphamap around a rock shifts onto the bare-ground layer, watching the weight sum, the transition
    ///     band's reach, the cluster and lone-rock paths, and the per-biome lookup so any missing ported formula fails
    /// </summary>
    public class ObjectSurroundTexturePainterTest
    {

        [SetUp]
        public void SetUp()
        {
            LoadMasterData();
        }
        private const int ClusterId = 7;
        private const int NoCluster = -1;

        [Test]
        public void AClusteredRockPullsTheNearbyPixelsOntoTheSurroundLayer()
        {
            var alphamap = CreateUniformAlphamap();
            Paint(alphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(ClusterId));

            // 岩の直下画素が裸地へ寄らないなら、クラスタ経路が一度も描いていない
            // If the pixel under the rock never shifts, the cluster path drew nothing at all
            Assert.That(alphamap[RockPixel, RockPixel, MudLayerIndex], Is.GreaterThan(0.5f));
        }

        [Test]
        public void PixelsWhoseSurroundLayerStartsEmptyKeepTheirWeightSum()
        {
            var alphamap = CreateUniformAlphamap();
            Paint(alphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(ClusterId));

            // 再正規化を落とすと足した重みぶんだけ合計が1を超え、Unityが割り戻して全レイヤーが薄くなる
            // Dropping the renormalization pushes the sum past 1 and Unity rescales it, thinning every layer
            // 合計が保たれるのは書き込み先のMud列が0で始まるこの盤面だけで、一般の保証ではない
            // The sum only survives because this board starts with an empty Mud column; it is not a general guarantee
            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
            {
                var sum = 0f;
                for (var layer = 0; layer < LayerCount; layer++) sum += alphamap[z, x, layer];
                Assert.That(sum, Is.EqualTo(1f).Within(1e-4f), $"z={z} x={x}");
            }
        }

        [Test]
        public void APixelWhoseSurroundLayerAlreadyHasWeightEndsAboveOne()
        {
            // 移植元は他レイヤー×(1-blend)＋書き込み先へ元の合計×blendなので、合計はS+blend*m（mは書き込み先の元の重み）
            // The source folds the others by (1-blend) and adds blend times the original total, so the sum becomes S + blend*m
            // desertはMudDryをtextureConfigに持つので、重なる岩の2回目以降は必ず m>0 になる
            // Desert lists MudDry in its textureConfig, so any second write on overlapping rocks always has m > 0
            var alphamap = CreateAlphamapWithSurroundWeight();
            Paint(alphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(ClusterId));

            // blendはノイズ込みなので結果から復元する。式を再実装せずに畳み方だけを固定するため
            // The blend carries noise, so it is recovered from the result to fix the fold without reimplementing the formula
            var blend = 1f - alphamap[RockPixel, RockPixel, 2] / PresetMainWeight;
            Assert.That(blend, Is.GreaterThan(0.5f), "岩の直下はコア帯なので大きく寄る");

            var sum = 0f;
            for (var layer = 0; layer < LayerCount; layer++) sum += alphamap[RockPixel, RockPixel, layer];

            Assert.That(sum, Is.EqualTo(1f + blend * PresetSurroundWeight).Within(1e-4f));
            Assert.That(sum, Is.GreaterThan(1f), "実データでは書き込み後の合計が1を超えうる");
        }

        [Test]
        public void PixelsBeyondTheTransitionBandKeepTheirOriginalWeights()
        {
            var alphamap = CreateUniformAlphamap();
            Paint(alphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(ClusterId));

            // 岩から最も遠い隅。遷移帯の打ち切りを外すと地形全面が泥になる
            // The corner furthest from the rock; removing the transition band's cutoff would turn the whole terrain to mud
            Assert.That(alphamap[0, 0, MudLayerIndex], Is.EqualTo(0f));
            Assert.That(alphamap[0, 0, 2], Is.EqualTo(1f));
        }

        [Test]
        public void ALoneRockPaintsTheSingleRockRadiusRatherThanTheTransitionBand()
        {
            // 単体経路の届く範囲はsingleRockRadius=8m(=1.6画素)だけで決まる。クラスタ経路へ流れると2画素先まで塗ってしまう
            // The lone path reaches only as far as singleRockRadius = 8m (1.6 pixels); the cluster path would paint two pixels out
            var loneAlphamap = CreateUniformAlphamap();
            Paint(loneAlphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(NoCluster));

            var clusteredAlphamap = CreateUniformAlphamap();
            Paint(clusteredAlphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(ClusterId));

            Assert.That(loneAlphamap[RockPixel, RockPixel, MudLayerIndex], Is.GreaterThan(0.2f));
            Assert.That(loneAlphamap[RockPixel, RockPixel + 2, MudLayerIndex], Is.EqualTo(0f));
            Assert.That(clusteredAlphamap[RockPixel, RockPixel + 2, MudLayerIndex], Is.GreaterThan(0.5f));
        }

        [Test]
        public void ARockWithoutBareGroundLeavesTheAlphamapAlone()
        {
            // 移植元はBoulder/Cliff名の岩だけ裸地化する。rockNoBareGroundの瓦礫が塗り始めるとメサ一帯が泥になる
            // The source repaints only Boulder/Cliff rocks; once rockNoBareGround rubble starts painting, the whole mesa turns to mud
            var clusteredAlphamap = CreateUniformAlphamap();
            Paint(clusteredAlphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(ClusterId, NoBareGroundStoneGuid));
            var loneAlphamap = CreateUniformAlphamap();
            Paint(loneAlphamap, CreateSurroundConfig(), CreateHeights(0f), CreateRock(NoCluster, NoBareGroundStoneGuid));

            Assert.That(clusteredAlphamap[RockPixel, RockPixel, MudLayerIndex], Is.EqualTo(0f));
            Assert.That(loneAlphamap[RockPixel, RockPixel, MudLayerIndex], Is.EqualTo(0f));
        }

        [Test]
        public void ADisabledSurroundConfigLeavesTheAlphamapAlone()
        {
            var alphamap = CreateUniformAlphamap();
            var disabledConfig = CreateSurroundConfig();
            disabledConfig.enabled = false;

            Paint(alphamap, disabledConfig, CreateHeights(0f), CreateRock(ClusterId));

            Assert.That(alphamap[RockPixel, RockPixel, MudLayerIndex], Is.EqualTo(0f));
        }

        [Test]
        public void TheClusterCentroidsWinningBiomeChoosesTheConfig()
        {
            // 重み列のオフセットや勝者判定がずれると隣のバイオームの設定で描かれる。届く範囲の違う2本で見分ける
            // A shifted weight column or winner check would draw with the neighbouring biome's config; two differing reaches separate them
            var narrowConfig = CreateSurroundConfig();
            narrowConfig.rockMeshBaseSize = 1f;
            narrowConfig.transitionRadius = 6f;
            var wideConfig = CreateSurroundConfig();

            var narrowAlphamap = CreateUniformAlphamap();
            PaintWithTwoBiomes(narrowAlphamap, narrowConfig, wideConfig, 0);

            var wideAlphamap = CreateUniformAlphamap();
            PaintWithTwoBiomes(wideAlphamap, narrowConfig, wideConfig, 1);

            Assert.That(narrowAlphamap[RockPixel, RockPixel + 2, MudLayerIndex], Is.EqualTo(0f));
            Assert.That(wideAlphamap[RockPixel, RockPixel + 2, MudLayerIndex], Is.GreaterThan(0.5f));
        }

        // 到達距離はpainterの内部事情になったので直接は測れない。境界を跨ぐ塗りはSplatmapRuntimeGeneratorSurroundWiringTestが挟み込む
        // The reach is now the painter's own business and cannot be measured directly; the seam behaviour is bracketed by SplatmapRuntimeGeneratorSurroundWiringTest
        private static void PaintWithTwoBiomes(
            float[,,] alphamap, SurroundTextureConfig firstConfig, SurroundTextureConfig secondConfig, int winningBiome)
        {
            ObjectSurroundTexturePainter.Apply(
                alphamap, CreateConfig(), CreateLayerTable(), new[] { firstConfig, secondConfig },
                CreateBiomeWeights(winningBiome), 2, CreateHeights(0f), new[] { CreateRock(7) }, Vector3.zero);
        }

        private static void Paint(
            float[,,] alphamap, SurroundTextureConfig surroundConfig, float[,] heights,
            MapObjectLayoutMessagePack stoneObject)
        {
            ObjectSurroundTexturePainter.Apply(
                alphamap, CreateConfig(), CreateLayerTable(), new[] { surroundConfig },
                CreateBiomeWeights(0), 1, heights, new[] { stoneObject }, Vector3.zero);
        }
    }
}
