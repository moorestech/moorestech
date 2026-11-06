using Client.Ui.Components.Panels;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// 面の縁フェードの mask（skit・hud の形）と α 補正が、端・途中・頂点 α の合成で Web と同じ濃さの並びになるかを確かめる（L0）
    /// Verifies the face edge-fade mask (skit and hud shapes) and alpha correction give the Web run of shades at ends, mid-fade and in vertex alpha (L0)
    /// </summary>
    public class GamePanelFaceOpacityRampTest
    {
        // --surface-navy の α（Web の α0.8 を 1-(1-a)^2.2 で補正した値）
        // The --surface-navy alpha (Web alpha 0.8 corrected by 1-(1-a)^2.2)
        private const float NavyFaceAlpha = 0.971f;

        // skit の形（左右と下端はフェードせず上端だけ 12px）と hud の形（4 辺 10px）
        // The skit shape (no side or bottom fade, top only 12px) and the hud shape (10px on all four sides)
        private const float Width = 400f;
        private const float Height = 160f;
        private const float SkitTopFade = 12f;
        private const float HudFade = 10f;

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

        [Test]
        public void skitの形では下端と左右端は濃いまま上端だけ透明へ落ちる()
        {
            // フェード幅 0 の辺で 0 で割らず重み 1 に保つ分岐が、skit の下端と左右端を硬く残す
            // The branch that keeps a zero-span side at weight 1 without dividing by zero keeps the skit bottom and sides hard
            Assert.AreEqual(1f, SkitWeight(Width / 2f, Height), 0.0001f);
            Assert.AreEqual(1f, SkitWeight(0f, Height / 2f), 0.0001f);
            Assert.AreEqual(1f, SkitWeight(Width, Height / 2f), 0.0001f);
            Assert.AreEqual(1f, SkitWeight(0f, Height), 0.0001f);
            Assert.AreEqual(0f, SkitWeight(Width / 2f, 0f), 0.0001f);
            Assert.AreEqual(0.5f, SkitWeight(Width / 2f, SkitTopFade / 2f), 0.0001f);
        }

        [Test]
        public void hudの形では4隅が透明で縁の中ほどは2辺の積になり中央は濃いまま()
        {
            Assert.AreEqual(0f, HudWeight(0f, 0f), 0.0001f);
            Assert.AreEqual(0f, HudWeight(Width, Height), 0.0001f);
            Assert.AreEqual(1f, HudWeight(Width / 2f, Height / 2f), 0.0001f);
            Assert.AreEqual(0.5f, HudWeight(Width / 2f, Height - HudFade / 2f), 0.0001f);
            Assert.AreEqual(0.25f, HudWeight(HudFade / 2f, HudFade / 2f), 0.0001f);
        }

        [Test]
        public void 頂点のαは縁の重みを線形に掛けずS7の式で補正した値になる()
        {
            // skit 上端の 50% 点。重み 0.5 を線形に掛けた 0.4855 ではなく OpacityAt(a, 0.5) = 0.6750 になる
            // The skit top 50% point: not the linear 0.4855 but OpacityAt(a, 0.5) = 0.6750
            var alpha = GamePanelFaceOpacityRamp.VertexAlpha(NavyFaceAlpha, Width / 2f, SkitTopFade / 2f, Width, Height, 0f, SkitTopFade, 0f);
            Assert.AreEqual(0.6750f, alpha, 0.002f);
            Assert.AreEqual(NavyFaceAlpha, GamePanelFaceOpacityRamp.VertexAlpha(NavyFaceAlpha, Width / 2f, Height, Width, Height, 0f, SkitTopFade, 0f), 0.0001f);
        }

        private static float SkitWeight(float x, float y)
        {
            return GamePanelFaceOpacityRamp.MaskWeightAt(x, y, Width, Height, 0f, SkitTopFade, 0f);
        }

        private static float HudWeight(float x, float y)
        {
            return GamePanelFaceOpacityRamp.MaskWeightAt(x, y, Width, Height, HudFade, HudFade, HudFade);
        }
    }
}
