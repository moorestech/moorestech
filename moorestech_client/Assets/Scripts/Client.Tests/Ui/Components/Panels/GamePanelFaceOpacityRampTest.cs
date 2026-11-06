using Client.Ui.Components.Panels;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// 面の縁フェードの α 補正が、両端でトークンと透明に戻り、途中で Web の α を S7 の式で補正した値になるかを確かめる（L0）
    /// Verifies the face edge-fade alpha correction returns the token and clear at the ends and the S7-corrected Web alpha in between (L0)
    /// </summary>
    public class GamePanelFaceOpacityRampTest
    {
        // --surface-navy の α（Web の α0.8 を 1-(1-a)^2.2 で補正した値）
        // The --surface-navy alpha (Web alpha 0.8 corrected by 1-(1-a)^2.2)
        private const float NavyFaceAlpha = 0.971f;

        [TestCase(0.971f)]
        [TestCase(0.9906f)]
        [TestCase(0.95f)]
        public void 重み1ではトークンのαそのものになる(float faceAlpha)
        {
            Assert.AreEqual(faceAlpha, GamePanelFaceOpacityRamp.OpacityAt(faceAlpha, 1f), 0.0001f);
        }

        [Test]
        public void 重み0では透明になる()
        {
            Assert.AreEqual(0f, GamePanelFaceOpacityRamp.OpacityAt(NavyFaceAlpha, 0f), 0.0001f);
        }

        [Test]
        public void 重み0点5ではWebのα0点4をS7の式で補正した値になる()
        {
            // Web は α0.8 の面を mask 0.5 で α0.4 にする。Linear 合成用には 1-(1-0.4)^2.2 = 0.6750
            // The Web turns the alpha-0.8 face into alpha 0.4 at mask 0.5; for Linear blending that is 1-(1-0.4)^2.2 = 0.6750
            Assert.AreEqual(0.6750f, GamePanelFaceOpacityRamp.OpacityAt(NavyFaceAlpha, 0.5f), 0.002f);
        }

        [Test]
        public void 途中の重みでは線形に掛けた値より濃い()
        {
            // 線形に掛けると暗い面が明るい世界の上で縁から遅れて濃くなる（スキット上端の 50% 点が約 9px ずれた）
            // A linear product darkens a dark face over a bright world late (the skit top 50% point drifted about 9px)
            for (var weight = 0.1f; weight < 1f; weight += 0.1f)
            {
                Assert.Greater(GamePanelFaceOpacityRamp.OpacityAt(NavyFaceAlpha, weight), NavyFaceAlpha * weight);
            }
        }
    }
}
