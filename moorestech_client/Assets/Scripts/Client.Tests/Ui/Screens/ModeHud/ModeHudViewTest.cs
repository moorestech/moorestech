using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Tests.Ui.Components;
using Client.Ui.Components.Panels;
using Client.Ui.Screens.ModeHud;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.ModeHud
{
    /// <summary>
    /// 配置 HUD の寸法・色・入力を奪わないこと（L1）
    /// Placement HUD sizes, colours and not taking input (L1)
    /// </summary>
    public class ModeHudViewTest : ScreenTestHost
    {
        // .mode-hud__detail が --localized-label-line-height（25px）を宣言するので LocalizedLabel は文の頭に行送りのタグを付ける（前例 TooltipFaceTest.LineHeightTag）
        // .mode-hud__detail declares --localized-label-line-height (25px), so LocalizedLabel prefixes the pitch tag (precedent TooltipFaceTest.LineHeightTag)
        public const string DetailLineHeightTag = "<line-height=25px>";

        private ModeHudView _view;

        [SetUp]
        public void MountModeHud()
        {
            _view = Mount(new ModeHudView());
        }

        /// <web-test file="e2e/tests/modeHud/operation-mode-hud.spec.ts">配置モードHUDを右上のクラフト枠で表示する</web-test>
        [Test]
        public void 配置モードHUDは右上のクラフト枠に題と対象と高さを出す()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();

            // 右上 24px・幅 288px
            // Top-right 24px, 288px wide
            var corner = Find<VisualElement>(_view, "placement-mode-hud");
            Assert.AreEqual(24f, corner.worldBound.yMin, 1f);
            Assert.AreEqual(1280f - 24f, corner.worldBound.xMax, 1f);
            Assert.AreEqual(288f, corner.worldBound.width, 1f);
            Assert.IsFalse(corner.worldBound.Contains(new Vector2(640f, 360f)));

            // 枠はクラフト・面はクラフトのトークン
            // Craft frame and craft face token
            var panel = Find<GamePanel>(_view, "placement-mode-hud-panel");
            Assert.IsTrue(panel.ClassListContains("game-panel--craft"));
            Assert.IsNotNull(panel.Q(className: "game-panel__craft-grip"));
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--game-panel-craft-face"), panel.resolvedStyle.backgroundColor, "craft face");

            // 題 12px・text-muted・字間 1px
            // Title 12px, text-muted, 1px spacing
            var label = Find<Label>(_view, "operation-mode-label");
            Assert.AreEqual("Placement Mode", label.text);
            Assert.AreEqual(12f, label.resolvedStyle.fontSize, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-muted"), label.resolvedStyle.color, "label");
            Assert.AreEqual(8.333f, label.resolvedStyle.letterSpacing, 0.01f);

            // 詳細 17px・白・行高 25px
            // Detail 17px, white, 25px line height
            var details = _view.Query<Label>("operation-mode-detail").ToList();
            Assert.AreEqual(new[] { DetailLineHeightTag + "Selected: Assembler", DetailLineHeightTag + "Height: 3" }, new[] { details[0].text, details[1].text });
            Assert.AreEqual(17f, details[0].resolvedStyle.fontSize, 0.01f);
            ThemeTokenFile.AssertSameColor(ThemeTokenFile.ResolveColor("--text-high-contrast"), details[0].resolvedStyle.color, "detail");
            Assert.AreEqual(25f, details[0].worldBound.height, 0.5f);
            Assert.AreEqual(5f, details[1].worldBound.yMin - details[0].worldBound.yMax, 0.5f);

            // 明暗どちらの世界の上でも詳細が読める（WCAG 4.5 以上）
            // Details stay readable over both light and dark worlds (WCAG 4.5 or more)
            foreach (var backdrop in new[] { Color.white, Color.black })
            {
                var face = ContrastRatio.Over(panel.resolvedStyle.backgroundColor, backdrop);
                Assert.GreaterOrEqual(ContrastRatio.Of(details[0].resolvedStyle.color, face), 4.5f);
            }
        }

        /// <web-test file="e2e/tests/system/commonHud.spec.ts">設置情報と削除警告帯を操作モードへ反映する</web-test>
        [Test]
        public void 設置では錨placement_hudに対象と高さが出て削除では帯2本と錨delete_hudが下端に出る()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            var placement = _view.Q(className: AnchorIds.PlacementHud);
            Assert.IsNotNull(placement, "錨 placement.hud");
            StringAssert.Contains("Assembler", AllText(placement));
            StringAssert.Contains("3", AllText(placement));

            _view.Render(ModeHudViewModelSamples.Delete(3));
            simulate.FrameUpdate();
            Assert.IsNull(_view.Q("placement-mode-hud"), "配置 HUD は木から外れる");
            var bands = _view.Query<VisualElement>("delete-mode-warning-band").ToList();
            Assert.AreEqual(2, bands.Count);
            Assert.AreEqual(720f, bands[1].worldBound.yMax, 0.5f);
            Assert.IsTrue(bands[1].ClassListContains(AnchorIds.DeleteHud));
        }

        [Test]
        public void 起動直後の非表示と表示の切り替えでエラーを出さない()
        {
            // Presenter と同じく載せた直後に Hidden を描き、数フレーム回しても LocalizedTextBinding の「文の無いラベル」が出ない
            // Like the presenter, render Hidden right after mounting; several frames later no textless-label error appears
            _view.Render(Client.Ui.State.ModeHud.ModeHudViewModel.Hidden);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            _view.Render(ModeHudViewModelSamples.Placement());
            _view.Render(ModeHudViewModelSamples.Delete(1));
            _view.Render(Client.Ui.State.ModeHud.ModeHudViewModel.Hidden);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void どの要素も入力を拾わない()
        {
            _view.Render(ModeHudViewModelSamples.Placement());
            simulate.FrameUpdate();
            _view.Query<VisualElement>().ForEach(element => Assert.AreEqual(PickingMode.Ignore, element.pickingMode, element.name));
        }

        private static string AllText(VisualElement root)
        {
            return string.Join(" ", root.Query<TextElement>().ToList().ConvertAll(element => element.text));
        }
    }
}
