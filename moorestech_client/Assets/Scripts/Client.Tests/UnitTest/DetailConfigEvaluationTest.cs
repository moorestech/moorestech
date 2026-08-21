using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Detail.Filter;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     Detail設定型そのものの評価式を検証する。移植時に取り違えやすい合成順とClampを固定する。
    ///     Verifies the detail config types' own evaluation formulas, pinning the composition order and clamping that porting easily confuses.
    /// </summary>
    public class DetailConfigEvaluationTest
    {
        [Test]
        public void TextureFilterUsesMatchedLayerWeightAndFallsBackToOtherTextureWeight()
        {
            // レイヤー0はエントリ一致で0.5、レイヤー1は未登録なのでotherTextureWeightの0.25が効く
            // Layer 0 matches an entry and uses 0.5; layer 1 is unregistered so otherTextureWeight 0.25 applies
            var matchedEntry = new DetailTextureFilter.TextureFilterEntry { layerAddressablePath = "addr/matched", weight = 0.5f };
            matchedEntry.SetLayerIndex(0);
            var textureFilter = new DetailTextureFilter
            {
                enabled = true,
                otherTextureWeight = 0.25f,
                entries = new[] { matchedEntry },
            };

            var splatmap = new float[1, 1, 2];
            splatmap[0, 0, 0] = 0.6f;
            splatmap[0, 0, 1] = 0.4f;

            var result = textureFilter.Evaluate(splatmap, 0, 0);

            Assert.That(result, Is.EqualTo(0.6f * 0.5f + 0.4f * 0.25f).Within(1e-5f));
        }

        [Test]
        public void NoiseLayerMultipliesAmplitudeBeforeAddingBalanceAndOffset()
        {
            // amplitude=0なら生ノイズは消え、balance+offsetだけが残る。
            // PlacementNoiseの式(生値+offset+balance)*amplitudeなら常に0になるので、両者を取り違えていれば必ず落ちる。
            // With amplitude 0 the raw noise vanishes and only balance+offset survive.
            // PlacementNoise's (raw+offset+balance)*amplitude would always yield 0, so confusing the two always fails here.
            var noiseOffsets = new[] { Vector2.zero };
            var layer = new DetailNoiseLayer
            {
                noiseType = MapNoiseType.Simple, frequency = 10f, amplitude = 0f, balance = 0.2f, offset = 0.1f,
            };

            Assert.That(layer.Sample(3f, 7f, noiseOffsets), Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void NoiseLayerClampsItsResultIntoZeroToOne()
        {
            var noiseOffsets = new[] { Vector2.zero };
            var overshooting = new DetailNoiseLayer
            {
                noiseType = MapNoiseType.Simple, frequency = 10f, amplitude = 0f, balance = 0f, offset = 2f,
            };
            var undershooting = new DetailNoiseLayer
            {
                noiseType = MapNoiseType.Simple, frequency = 10f, amplitude = 0f, balance = 0f, offset = -2f,
            };

            Assert.That(overshooting.Sample(3f, 7f, noiseOffsets), Is.EqualTo(1f));
            Assert.That(undershooting.Sample(3f, 7f, noiseOffsets), Is.EqualTo(0f));
        }

        [Test]
        public void InactiveNoiseLayerIsNeutral()
        {
            var inactiveLayer = new DetailNoiseLayer { noiseType = MapNoiseType.None, amplitude = 0f, offset = -5f };

            Assert.That(inactiveLayer.Sample(0f, 0f, new[] { Vector2.zero }), Is.EqualTo(1f));
        }
    }
}
