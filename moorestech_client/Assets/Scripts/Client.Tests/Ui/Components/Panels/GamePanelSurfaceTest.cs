using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Theme;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Panels
{
    /// <summary>
    /// GamePanelSurface の hud（余白・4 辺のフェード・内容で伸びる）と skit（下端の全幅帯・上端だけのフェード）を確かめる（L1）
    /// Verifies GamePanelSurface hud (padding, four-edge fade, content height) and skit (full-width bottom band, top-only fade) (L1)
    /// </summary>
    public class GamePanelSurfaceTest : UiTestHost
    {
        private const float ContentHeight = 40f;

        /// <web-test file="src/shared/ui/GamePanel/hudVariantDesign.test.ts">hudのpaddingセレクタは.panelより詳細度で勝ち、import順に依存しない</web-test>
        [Test]
        public void hudの余白はhud_panel_paddingで内容の高さで伸びる()
        {
            var surface = AddHud();
            var padding = ThemeTokenFile.ResolvePixels("--hud-panel-padding");
            var body = surface.Q(className: "game-panel-surface__body").worldBound;
            Assert.AreEqual(surface.worldBound.x + padding, body.x, 0.5f);
            Assert.AreEqual(surface.worldBound.y + padding, body.y, 0.5f);
            Assert.AreEqual(ContentHeight + padding * 2f, surface.worldBound.height, 0.5f);
        }

        /// <web-test file="src/shared/ui/GamePanel/hudVariantDesign.test.ts">hudの面色とフェード幅はパネル面と同じ基底トークンを直接引く</web-test>
        [Test]
        public void hudの面はsurface_navyとpanel_edge_fadeを4辺に直接引く()
        {
            var face = AddHud().Q(className: "game-panel-surface__face");
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(face.customStyle, "--game-panel-face", out var faceColor));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--surface-navy"), faceColor, "--game-panel-face");
            var edgeFade = ThemeTokenFile.ResolvePixels("--panel-edge-fade");
            foreach (var knob in new[] { "--game-panel-edge-fade", "--game-panel-top-fade", "--game-panel-bottom-fade" })
            {
                Assert.IsTrue(UssCustomStyleReader.TryReadPixels(face.customStyle, knob, out var fade), knob);
                Assert.AreEqual(edgeFade, fade, 0.0001f, knob);
            }
        }

        /// <web-test file="src/shared/ui/GamePanel/hudVariantDesign.test.ts">既定面は肯定形のvariant判定で敷き、重なり順は全variant共通にする</web-test>
        [Test]
        public void hudの面は外寸いっぱいで本文より背後にあり三角と罫線を持たない()
        {
            var surface = AddHud();
            var face = surface.Q(className: "game-panel-surface__face");
            var body = surface.Q(className: "game-panel-surface__body");
            Assert.AreEqual(surface.worldBound.x, face.worldBound.x, 0.5f);
            Assert.AreEqual(surface.worldBound.yMax, face.worldBound.yMax, 0.5f);
            Assert.Less(surface.hierarchy.IndexOf(face), surface.hierarchy.IndexOf(body));
            Assert.IsNull(surface.Q(className: "game-panel__bottom-deco"));
            Assert.IsNull(surface.Q(className: "game-panel__deco-line"));
        }

        [Test]
        public void skitは親の下端に全幅でskit_window_heightの高さ()
        {
            var surface = new GamePanelSurface("test-skit-surface", GamePanelSurfaceKind.Skit);
            rootVisualElement.Add(surface);
            simulate.FrameUpdate();
            var root = rootVisualElement.worldBound;
            Assert.AreEqual(root.width, surface.worldBound.width, 0.5f);
            Assert.AreEqual(root.yMax, surface.worldBound.yMax, 0.5f);
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--skit-window-height"), surface.worldBound.height, 0.5f);
        }

        [Test]
        public void skitの面はskit_window_faceで上端だけフェードする()
        {
            var surface = new GamePanelSurface("test-skit-surface", GamePanelSurfaceKind.Skit);
            rootVisualElement.Add(surface);
            simulate.FrameUpdate();
            var face = surface.Q(className: "game-panel-surface__face").customStyle;
            Assert.IsTrue(UssCustomStyleReader.TryReadColor(face, "--game-panel-face", out var faceColor));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--skit-window-face"), faceColor, "--game-panel-face");
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(face, "--game-panel-top-fade", out var topFade));
            Assert.AreEqual(ThemeTokenFile.ResolvePixels("--skit-window-top-fade"), topFade, 0.0001f);
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(face, "--game-panel-edge-fade", out var sideFade));
            Assert.AreEqual(0f, sideFade, 0.0001f);
            Assert.IsTrue(UssCustomStyleReader.TryReadPixels(face, "--game-panel-bottom-fade", out var bottomFade));
            Assert.AreEqual(0f, bottomFade, 0.0001f);
        }

        [Test]
        public void 面は拾わず描画でエラーを出さない()
        {
            var surface = AddHud();
            rootVisualElement.Add(new GamePanelSurface("test-skit-surface", GamePanelSurfaceKind.Skit));
            simulate.FrameUpdate();
            Assert.AreEqual(PickingMode.Ignore, surface.Q(className: "game-panel-surface__face").pickingMode);
            LogAssert.NoUnexpectedReceived();
        }

        private GamePanelSurface AddHud()
        {
            // 幅だけ決めた入れ物に置き、高さは内容で決まるようにする（Web のチャレンジ HUD と同じ）
            // Put it in a width-only container so the height follows the content (like the Web challenge HUD)
            var container = new VisualElement();
            container.style.width = 278;
            var surface = new GamePanelSurface("test-hud-surface", GamePanelSurfaceKind.Hud);
            var content = new VisualElement();
            content.style.height = ContentHeight;
            surface.Add(content);
            container.Add(surface);
            rootVisualElement.Add(container);
            simulate.FrameUpdate();
            return surface;
        }
    }
}
