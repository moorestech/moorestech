using Client.Ui.Foundation.Theme;
using NUnit.Framework;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 重み付き α が端・途中で Web と同じ濃さ並びか（L0）
    /// Weighted alpha keeps the Web shade run at ends and mid-way (L0)
    /// </summary>
    public class LinearBlendOpacityTest
    {
        // --surface-navy の α（Web の α0.8 を 1-(1-a)^1.85 で補正した値）
        // The --surface-navy alpha (Web alpha 0.8 corrected by 1-(1-a)^1.85)
        private const float NavyFaceAlpha = 0.9491f;

        [TestCase(0.9491f)]
        [TestCase(0.9802f)]
        [TestCase(0.95f)]
        public void 重み1では補正済みのαそのものになる(float correctedAlpha)
        {
            Assert.AreEqual(correctedAlpha, LinearBlendOpacity.OpacityAt(correctedAlpha, 1f), 0.0001f);
        }

        [Test]
        public void 重み0では透明になる()
        {
            Assert.AreEqual(0f, LinearBlendOpacity.OpacityAt(NavyFaceAlpha, 0f), 0.0001f);
        }

        [Test]
        public void 重み0点5ではWebのα0点4をS7の式で補正した値になる()
        {
            // Web は α0.8 の面を重み 0.5 で α0.4 にする。Linear 合成用には 1-(1-0.4)^1.85 = 0.6113
            // The Web turns the alpha-0.8 face into alpha 0.4 at weight 0.5; for Linear blending that is 1-(1-0.4)^1.85 = 0.6113
            Assert.AreEqual(0.6113f, LinearBlendOpacity.OpacityAt(NavyFaceAlpha, 0.5f), 0.002f);
        }

        [Test]
        public void 途中の重みでは線形に掛けた値より濃い()
        {
            // 線形に掛けると暗い面が明るい世界の上で縁から遅れて濃くなる（スキット上端の 50% 点が約 9px ずれた）
            // A linear product darkens a dark face over a bright world late (the skit top 50% point drifted about 9px)
            for (var weight = 0.1f; weight < 1f; weight += 0.1f)
            {
                Assert.Greater(LinearBlendOpacity.OpacityAt(NavyFaceAlpha, weight), NavyFaceAlpha * weight);
            }
        }
    }
}
