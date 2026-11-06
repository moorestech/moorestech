using System.IO;
using Client.Ui.Components.Decor;
using Client.Ui.Components.Panels;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 操作モード HUD は共通のクラフト枠と帯で組み、色と寸法をトークン経由に限る（L1。Web の modeHudDesign.test.ts）
    /// The operation-mode HUD is built from the shared craft frame and bands, with colours and sizes only through tokens (L1; Web modeHudDesign.test.ts)
    /// </summary>
    public class ModeHudDesignTest : ScreenTestHost
    {
        private const string ModeHudUssPath = "Scripts/Client.Ui/Screens/ModeHud/ModeHud.uss";
        private const string TokensUssPath = "Scripts/Client.Ui/Foundation/Theme/Tokens.uss";

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">配置HUDをMantineではなく共通クラフト枠で構成する</web-test>
        [Test]
        public void 配置HUDは共通のクラフト枠とHUD見出しで組む()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Placement());
            var panel = Find<GamePanel>(view, "placement-mode-hud-panel");
            Assert.IsTrue(panel.ClassListContains("game-panel--craft"));
            Assert.IsNotNull(panel.Q<HudSectionHeading>());
            Assert.IsNotNull(panel.Q(className: "hud-section-heading__rule"));
        }

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">削除表示を説明パネルではなく上下の警告帯で構成する</web-test>
        [Test]
        public void 削除表示はパネルを持たず帯2本で組む()
        {
            var view = Mount(new ModeHudView());
            view.Render(ModeHudViewModelSamples.Delete(1));
            var warning = Find<VisualElement>(view, "delete-mode-warning");
            Assert.IsNull(warning.Q<GamePanel>());
            Assert.AreEqual(2, warning.Query<VisualElement>("delete-mode-warning-band").ToList().Count);
        }

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">機能CSSの色と寸法をトークン経由に限定する</web-test>
        [Test]
        public void 画面のUSSは色をトークン経由だけで書き帯の色と文字色のトークンを使う()
        {
            var uss = Read(ModeHudUssPath);
            foreach (var token in new[] { "var(--text-high-contrast)", "var(--delete-mode-warning-yellow)", "var(--delete-mode-warning-black)", "var(--operation-hud-top)", "var(--operation-hud-right)", "var(--operation-hud-width)", "var(--delete-mode-warning-band-height)" })
            {
                StringAssert.Contains(token, uss);
            }
            // 色リテラルの禁止は L2 UiStyleConventionTest が全 USS で見る。ここは画面の USS に色の直書きが無いことだけ重ねて見る
            // L2 UiStyleConventionTest bans colour literals in all USS; here only re-check this screen's USS
            StringAssert.DoesNotMatch(@":\s*(white|red|#[0-9a-fA-F]{3,8}|rgba?\()", uss);
        }

        /// <web-test file="src/features/modeHud/modeHudDesign.test.ts">操作モードHUDの視覚寸法を固定長トークンへ集約する</web-test>
        [Test]
        public void 寸法は固定長のトークンにある()
        {
            var tokens = Read(TokensUssPath);
            foreach (var name in new[] { "--operation-hud-right:", "--operation-hud-top:", "--operation-hud-width:", "--operation-hud-label-font-size:", "--operation-hud-detail-font-size:", "--delete-mode-warning-band-height:", "--delete-mode-warning-stripe-size:", "--delete-mode-warning-stripe-angle:" })
            {
                StringAssert.Contains(name, tokens);
            }
            StringAssert.DoesNotContain("--operation-hud-text-shadow:", tokens);
        }

        private static string Read(string pathUnderAssets)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, pathUnderAssets));
        }
    }
}
