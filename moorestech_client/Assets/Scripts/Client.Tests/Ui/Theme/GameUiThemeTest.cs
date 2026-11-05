using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Theme
{
    /// <summary>
    /// 本番テーマで既定コントロールが暗色トークンに解決されることを確かめる（L1）
    /// Verifies default controls resolve to the dark control tokens under the production theme (L1)
    /// </summary>
    public class GameUiThemeTest : UiTestHost
    {
        [Test]
        public void ButtonAndTextFieldUseControlTokens()
        {
            var button = new Button { text = "x" };
            var field = new TextField();
            rootVisualElement.Add(button);
            rootVisualElement.Add(field);
            simulate.FrameUpdate();

            // ControlTokens.uss の --control-face と --control-input-face
            // --control-face and --control-input-face from ControlTokens.uss
            AssertColor("#353b45", button.resolvedStyle.backgroundColor);
            var input = field.Q(className: "unity-base-text-field__input");
            AssertColor("#1f2329", input.resolvedStyle.backgroundColor);
        }

        [Test]
        public void TokensResolveFromThemeWithoutRootStyleSheets()
        {
            // ルートにトークン USS を当てなくても、テーマ TSS の :root から var() が解決される
            // var() resolves from the theme TSS :root without token USS on the root
            // 基底が当てるテーマ 1 枚だけで、トークン USS は個別に載っていない
            // Only the single theme sheet from the base is on the root, no individual token USS
            Assert.AreEqual(1, rootVisualElement.styleSheets.count);
            Assert.AreSame(themeStyleSheet, rootVisualElement.styleSheets[0]);
            // var() だけで色を書く Theme.uss の Button が --control-face に解決されている
            // A Button using var() in Theme.uss resolves to --control-face
            var button = new Button();
            rootVisualElement.Add(button);
            simulate.FrameUpdate();
            AssertColor("#353b45", button.resolvedStyle.backgroundColor);
        }

        [Test]
        public void SliderInputKeepsNonZeroHeight()
        {
            // 既定テーマの入力部の高さ 0 の癖を Theme.uss が直している
            // Theme.uss fixes the default theme's zero-height slider input
            var slider = new Slider(0, 1);
            rootVisualElement.Add(slider);
            simulate.FrameUpdate();
            Assert.Greater(slider.Q(className: "unity-base-slider__input").resolvedStyle.height, 0f);
        }

        private static void AssertColor(string expectedHex, Color actual)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(expectedHex, out var expected), expectedHex);
            Assert.AreEqual(expected.r, actual.r, 0.01f);
            Assert.AreEqual(expected.g, actual.g, 0.01f);
            Assert.AreEqual(expected.b, actual.b, 0.01f);
        }
    }
}
