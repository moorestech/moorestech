using System;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;
using GenDetail = Mooresmaster.Model.BiomeDetailConfigModule;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     マスタのbiomeDetailConfigの全フィールドが実行時型へ到達することを検証する。
    ///     写し漏れは例外にも空リストにもならず「なんとなく分布が違う」形でしか現れないため、ここで固定する。
    ///     Verifies every biomeDetailConfig field reaches the runtime types. A dropped field yields neither an
    ///     exception nor an empty list, only a subtly different distribution, so it is pinned here.
    /// </summary>
    public class DetailRuntimeConfigFactoryTest
    {
        [Test]
        public void CarriesEveryPrototypeConfigField()
        {
            var entry = Build().entries[0];
            var prototypeConfig = entry.prototypeConfig;

            Assert.That(prototypeConfig.prototypeMeshAddressablePath, Is.EqualTo("addr/mesh"));
            Assert.That(prototypeConfig.prototypeTextureAddressablePath, Is.EqualTo("addr/tex"));
            Assert.That(prototypeConfig.usePrototypeMesh, Is.False);
            Assert.That(prototypeConfig.renderMode, Is.EqualTo(DetailRenderMode.VertexLit));
            Assert.That(prototypeConfig.minWidth, Is.EqualTo(1.1f));
            Assert.That(prototypeConfig.maxWidth, Is.EqualTo(1.2f));
            Assert.That(prototypeConfig.minHeight, Is.EqualTo(1.3f));
            Assert.That(prototypeConfig.maxHeight, Is.EqualTo(1.4f));
            Assert.That(prototypeConfig.alignToGround, Is.EqualTo(1.5f));
            Assert.That(prototypeConfig.positionJitter, Is.EqualTo(1.6f));
            Assert.That(prototypeConfig.targetCoverage, Is.EqualTo(1.7f));
            Assert.That(prototypeConfig.holeEdgePadding, Is.EqualTo(1.8f));
            Assert.That(prototypeConfig.noiseSeed, Is.EqualTo(19));
            Assert.That(prototypeConfig.noiseSpread, Is.EqualTo(2.0f));
            Assert.That(prototypeConfig.dryColor, Is.EqualTo(new Color(0.1f, 0.2f, 0.3f, 0.4f)));
            Assert.That(prototypeConfig.healthyColor, Is.EqualTo(new Color(0.5f, 0.6f, 0.7f, 0.8f)));
            Assert.That(prototypeConfig.useInstancing, Is.True);
            Assert.That(prototypeConfig.useDensityScaling, Is.False);
        }

        [Test]
        public void CarriesTextureFilterLayerAddressOfEveryEntry()
        {
            // アドレスを落とすとStep3がSetLayerを呼べず、Evaluateが永久に一致せず全レイヤーがotherTextureWeightへ倒れる
            // Dropping the address leaves Step 3 unable to call SetLayer, so Evaluate never matches and every layer falls to otherTextureWeight
            var textureFilter = Build().entries[0].textureFilter;

            Assert.That(textureFilter.enabled, Is.True);
            Assert.That(textureFilter.otherTextureWeight, Is.EqualTo(0.35f));
            Assert.That(textureFilter.entries.Length, Is.EqualTo(2));
            Assert.That(textureFilter.entries[0].layerAddressablePath, Is.EqualTo("addr/grass"));
            Assert.That(textureFilter.entries[0].weight, Is.EqualTo(0.25f));
            Assert.That(textureFilter.entries[1].layerAddressablePath, Is.EqualTo("addr/rock"));
            Assert.That(textureFilter.entries[1].weight, Is.EqualTo(-0.5f));
        }

        [Test]
        public void ThrowsWhenATextureFilterEntryHasNoLayerAddress()
        {
            var generated = CreateConfig(CreateTextureFilter(CreateTextureFilterEntry(string.Empty, 1f)));

            Assert.Throws<InvalidOperationException>(() => DetailRuntimeConfigFactory.Build(generated));
        }

        [Test]
        public void DoesNotThrowWhenADisabledTextureFilterEntryHasNoLayerAddress()
        {
            // disabledならEvaluateが早期脱出し出力に影響しないため、空アドレスは整備漏れ扱いしない
            // Disabled entries never reach Evaluate's matching logic, so an empty address there is not a data gap
            var generated = CreateConfig(new GenDetail.TextureFilter(false, 0.35f, new[] { CreateTextureFilterEntry(string.Empty, 1f) }));

            Assert.DoesNotThrow(() => DetailRuntimeConfigFactory.Build(generated));
        }

        [Test]
        public void CarriesEachFilterSlotWithoutCrossWiring()
        {
            var entry = Build().entries[0];

            // 各枠の固有値で取り違えを検知
            // Use unique slot values to detect swaps
            Assert.That(entry.slopeFilter.weight, Is.EqualTo(0.51f));
            Assert.That(entry.curvatureFilter.weight, Is.EqualTo(0.52f));
            Assert.That(entry.angleFilter.weight, Is.EqualTo(0.53f));
            Assert.That(entry.treeDistanceFilter.weight, Is.EqualTo(0.54f));
            Assert.That(entry.objectDistanceFilter.weight, Is.EqualTo(0.55f));

            Assert.That(entry.slopeFilter.enabled, Is.True);
            Assert.That(entry.slopeFilter.mode, Is.EqualTo(DetailFilter.Mode.Simple));
            Assert.That(entry.slopeFilter.range, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(entry.slopeFilter.smoothness, Is.EqualTo(new Vector2(5f, 6f)));
            Assert.That(entry.slopeFilter.noise.noiseType, Is.EqualTo(MapNoiseType.FBM));
            Assert.That(entry.slopeFilter.noise.frequency, Is.EqualTo(7f));
            Assert.That(entry.slopeFilter.noise.amplitude, Is.EqualTo(8f));
            Assert.That(entry.slopeFilter.noise.offset, Is.EqualTo(9f));
            Assert.That(entry.slopeFilter.noise.balance, Is.EqualTo(10f));

            // curveモードのフィルタはキーフレームがAnimationCurveへ再構築される
            // A curve-mode filter has its keyframes rebuilt into an AnimationCurve
            Assert.That(entry.curvatureFilter.mode, Is.EqualTo(DetailFilter.Mode.Curve));
            Assert.That(entry.curvatureFilter.curve.length, Is.EqualTo(1));
            Assert.That(entry.curvatureFilter.curve.keys[0].time, Is.EqualTo(0.25f));
            Assert.That(entry.curvatureFilter.curve.keys[0].value, Is.EqualTo(0.75f));
            // inTangent/outTangentが両方0だと既定接線と区別できず写し漏れがすり抜けるため、異なる非ゼロ値を与える
            // Both tangents defaulting to 0 would be indistinguishable from the Keyframe default, so distinct non-zero values are used
            Assert.That(entry.curvatureFilter.curve.keys[0].inTangent, Is.EqualTo(1.5f));
            Assert.That(entry.curvatureFilter.curve.keys[0].outTangent, Is.EqualTo(2.5f));
        }

        [Test]
        public void CarriesEntryLevelAndConfigLevelFields()
        {
            var config = Build();
            var entry = config.entries[0];

            Assert.That(config.filterRejectThreshold, Is.EqualTo(0.02f));
            Assert.That(config.borderMargin, Is.EqualTo(3.5f));
            Assert.That(entry.weight, Is.EqualTo(0.9f));
            Assert.That(entry.weightRange, Is.EqualTo(new Vector2(0.1f, 0.95f)));
            Assert.That(entry.maxDensity, Is.EqualTo(11));
            Assert.That(entry.occludedByOthers, Is.True);

            // 3層それぞれに異なる数値を与え、noiseType頼みではなく層の取り違えそのものを検知する
            // Each of the three layers gets distinct numbers so a swap is caught directly, not only via noiseType
            Assert.That(entry.noiseStack.primary.noiseType, Is.EqualTo(MapNoiseType.Worley));
            Assert.That(entry.noiseStack.primary.frequency, Is.EqualTo(7f));
            Assert.That(entry.noiseStack.primary.amplitude, Is.EqualTo(8f));
            Assert.That(entry.noiseStack.primary.offset, Is.EqualTo(9f));
            Assert.That(entry.noiseStack.primary.balance, Is.EqualTo(10f));
            Assert.That(entry.noiseStack.secondary.noiseType, Is.EqualTo(MapNoiseType.Simple));
            Assert.That(entry.noiseStack.secondary.frequency, Is.EqualTo(11f));
            Assert.That(entry.noiseStack.secondary.amplitude, Is.EqualTo(12f));
            Assert.That(entry.noiseStack.secondary.offset, Is.EqualTo(13f));
            Assert.That(entry.noiseStack.secondary.balance, Is.EqualTo(14f));
            Assert.That(entry.noiseStack.secondaryOp, Is.EqualTo(NoiseOp.Overlay));
            Assert.That(entry.noiseStack.tertiary.noiseType, Is.EqualTo(MapNoiseType.WormFBM));
            Assert.That(entry.noiseStack.tertiary.frequency, Is.EqualTo(15f));
            Assert.That(entry.noiseStack.tertiary.amplitude, Is.EqualTo(16f));
            Assert.That(entry.noiseStack.tertiary.offset, Is.EqualTo(17f));
            Assert.That(entry.noiseStack.tertiary.balance, Is.EqualTo(18f));
            Assert.That(entry.noiseStack.tertiaryOp, Is.EqualTo(NoiseOp.Min));
        }

        private static BiomeDetailConfig Build()
        {
            return DetailRuntimeConfigFactory.Build(CreateConfig(CreateTextureFilter(
                CreateTextureFilterEntry("addr/grass", 0.25f), CreateTextureFilterEntry("addr/rock", -0.5f))));
        }

        private static GenDetail.BiomeDetailConfig CreateConfig(GenDetail.TextureFilter textureFilter)
        {
            var prototypeConfig = new GenDetail.PrototypeConfig(
                "addr/mesh", "addr/tex", false, "VertexLit",
                1.1f, 1.2f, 1.3f, 1.4f, 1.5f, 1.6f, 1.7f, 1.8f,
                19, 2.0f, new Vector4(0.1f, 0.2f, 0.3f, 0.4f), new Vector4(0.5f, 0.6f, 0.7f, 0.8f), true, false);

            var noiseStack = new GenDetail.NoiseStack(
                CreateNoiseLayer("Worley", 7f, 8f, 9f, 10f), CreateNoiseLayer("Simple", 11f, 12f, 13f, 14f), "Overlay",
                CreateNoiseLayer("WormFBM", 15f, 16f, 17f, 18f), "Min");

            var entry = new GenDetail.DetailEntryElement(
                0, prototypeConfig, 0.9f, new Vector2(0.1f, 0.95f), 11, true, noiseStack,
                CreateFilter(0.51f, "Simple"), CreateFilter(0.52f, "Curve"), CreateFilter(0.53f, "Simple"),
                CreateFilter(0.54f, "Simple"), CreateFilter(0.55f, "Simple"), textureFilter);

            return new GenDetail.BiomeDetailConfig(new[] { entry }, 0.02f, 3.5f);
        }

        private static GenDetail.TextureFilter CreateTextureFilter(params GenDetail.TextureFilterEntryElement[] entries)
        {
            return new GenDetail.TextureFilter(true, 0.35f, entries);
        }

        private static GenDetail.TextureFilterEntryElement CreateTextureFilterEntry(string layerAddress, float weight)
        {
            return new GenDetail.TextureFilterEntryElement(0, layerAddress, weight);
        }

        private static Mooresmaster.Model.DetailNoiseLayerModule.DetailNoiseLayer CreateNoiseLayer(
            string noiseType, float frequency, float amplitude, float offset, float balance)
        {
            return new Mooresmaster.Model.DetailNoiseLayerModule.DetailNoiseLayer(noiseType, frequency, amplitude, offset, balance);
        }

        private static Mooresmaster.Model.DetailFilterModule.DetailFilter CreateFilter(float weight, string mode)
        {
            return new Mooresmaster.Model.DetailFilterModule.DetailFilter(
                true, mode, weight, new Vector2(3f, 4f), new Vector2(5f, 6f), CreateNoiseLayer("FBM", 7f, 8f, 9f, 10f),
                new[] { new Mooresmaster.Model.DetailFilterModule.CurveElement(0, 0.25f, 0.75f, 1.5f, 2.5f) });
        }
    }
}
