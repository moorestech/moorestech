using Client.Ui.Components.Panels;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// 面の縁フェード mask と頂点 α の濃さ並び（L0）
    /// Verifies the face edge-fade mask and vertex alpha shade run (L0)
    /// </summary>
    public class GamePanelFaceOpacityRampTest
    {
        // --surface-navy の α（Web の α0.8 のまま。UI は sRGB 空間で合成する: A-180）
        // The --surface-navy alpha (the Web alpha 0.8; the UI blends in sRGB space: A-180)
        private const float NavyFaceAlpha = 0.8f;

        // skit の形（左右と下端はフェードせず上端だけ 12px）と hud の形（4 辺 10px）
        // The skit shape (no side or bottom fade, top only 12px) and the hud shape (10px on all four sides)
        private const float Width = 400f;
        private const float Height = 160f;
        private const float SkitTopFade = 12f;
        private const float HudFade = 10f;

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
        public void 頂点のαは面のαに縁の重みを線形に掛けた値になる()
        {
            // skit 上端の 50% 点。Web の mask と同じく重み 0.5 を線形に掛けて 0.4
            // The skit top 50% point: the weight 0.5 is multiplied linearly as the Web mask does, giving 0.4
            var alpha = GamePanelFaceOpacityRamp.VertexAlpha(NavyFaceAlpha, Width / 2f, SkitTopFade / 2f, Width, Height, 0f, SkitTopFade, 0f);
            Assert.AreEqual(0.4f, alpha, 0.002f);
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
