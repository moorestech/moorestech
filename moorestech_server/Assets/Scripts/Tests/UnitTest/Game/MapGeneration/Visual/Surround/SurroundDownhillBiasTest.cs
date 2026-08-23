using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Surround;
using NUnit.Framework;
using static Tests.UnitTest.Game.MapGeneration.Visual.Surround.SurroundTestFixtures;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Surround
{
    /// <summary>
    ///     傾斜バイアスの倍率と、それが実際にalphamapへ効いているかを検証する。倍率が1に潰れると裸地が岩を中心に
    ///     真円で並び、斜面を下る見え方が消える
    ///     Verifies the slope bias factor and that it actually reaches the alphamap; collapsed to 1 the bare ground
    ///     forms perfect circles around each rock and stops running down slopes
    /// </summary>
    public class SurroundDownhillBiasTest
    {

        [SetUp]
        public void SetUp()
        {
            LoadMasterData();
        }
        // +x方向に上がる斜面。下り方向は-x
        // A slope rising along +x, so downhill points along -x
        private const float SlopeAlongX = 0.6f;

        // コア帯の下限は coreBlendMin * 0.7、遷移帯の上限は transitionBlendMax。両者は重ならない
        // The core band floors at coreBlendMin * 0.7 and the transition band tops out at transitionBlendMax; the two never overlap
        private const float CoreBandFloor = 0.56f;
        private const float TransitionBandCeiling = 0.5f;

        [Test]
        public void FlatGroundLeavesTheDistanceUncompressed()
        {
            var bias = Compute(CreateHeights(0f), 10f);

            Assert.That(bias, Is.EqualTo(1f));
        }

        [Test]
        public void ThePixelDirectlyDownhillCompressesTheDistanceByHalf()
        {
            // 移植元の 1 + Clamp01(dot) * 0.5。真下りで内積1になり1.5倍まで裸地が伸びる
            // The source's 1 + Clamp01(dot) * 0.5; straight downhill the dot reaches 1 and stretches bare ground 1.5x
            var bias = Compute(CreateHeights(SlopeAlongX), 10f);

            Assert.That(bias, Is.EqualTo(1.5f).Within(1e-3f));
        }

        [Test]
        public void ThePixelDirectlyUphillIsNotStretchedAtAll()
        {
            // Clamp01が負の内積を切る。切らないと上り側の裸地が縮んで岩が卵形に見える
            // Clamp01 cuts the negative dot; without it the uphill bare ground would shrink and the rock would look egg-shaped
            var bias = Compute(CreateHeights(SlopeAlongX), 35f);

            Assert.That(bias, Is.EqualTo(1f));
        }

        [Test]
        public void TheDownhillSideOfARockGetsMoreBareGroundThanTheUphillSide()
        {
            // 岩から等距離の2画素で比べる。バイアスが効くと下り側だけがコア帯に入り、上り側は遷移帯に留まる
            // Compared at two pixels equidistant from the rock; with the bias the downhill one enters the core band while the uphill one stays in the transition
            var surroundConfig = CreateSurroundConfig();
            surroundConfig.rockMeshBaseSize = 1f;
            surroundConfig.coreRadius = 3f;

            var alphamap = CreateUniformAlphamap();
            ObjectSurroundTexturePainter.Apply(
                alphamap, CreateConfig(), CreateLayerTable(), new[] { surroundConfig },
                CreateBiomeWeights(0), 1, CreateHeights(SlopeAlongX), new[] { CreateRock(7) }, UnityEngine.Vector3.zero);

            // 帯そのものが違うので閾値で切れる。ノイズ倍率の差だけを見ると倍率1でもたまたま通ってしまう
            // The two land in different bands, so a threshold separates them; comparing only noise could pass even at a flat bias
            var downhill = alphamap[RockPixel, RockPixel - 1, MudLayerIndex];
            var uphill = alphamap[RockPixel, RockPixel + 1, MudLayerIndex];

            Assert.That(downhill, Is.GreaterThan(CoreBandFloor), "下り側はコア帯へ入る");
            Assert.That(uphill, Is.LessThan(TransitionBandCeiling), "上り側は遷移帯に留まる");
        }

        // 岩は画素4の中心。sampleWorldXだけを動かして同じ高さ列の上で比べる
        // The rock sits on pixel 4's center; only sampleWorldX moves so the comparison stays on one height row
        private static float Compute(float[,] heights, float sampleWorldX)
        {
            var config = CreateConfig();
            var footprints = new List<(float x, float z, float radius)> { (RockLocalPosition, RockLocalPosition, 0f) };

            var heightX = UnityEngine.Mathf.Clamp(
                UnityEngine.Mathf.RoundToInt(sampleWorldX / TerrainSize * (Resolution - 1)), 0, Resolution - 1);
            var heightZ = UnityEngine.Mathf.Clamp(
                UnityEngine.Mathf.RoundToInt(RockLocalPosition / TerrainSize * (Resolution - 1)), 0, Resolution - 1);

            return SurroundDownhillBias.Compute(
                config, heights, Resolution, heightX, heightZ, sampleWorldX, RockLocalPosition, footprints);
        }
    }
}
