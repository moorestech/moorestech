using Client.Input;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.Foundation.Input;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using static Client.Tests.Ui.Components.Controls.RecipeAction.RecipeActionButtonAssert;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Controls.RecipeAction
{
    /// <summary>
    /// 押下・寸法・3 状態・沈み（L1）
    /// Press, size, three states and sink (L1)
    /// </summary>
    public class RecipeActionButtonTest : RecipeActionButtonFixture
    {

        [Test]
        public void クリックで押下を1回流しキーと無効では押されずフォーカスを持たない()
        {
            var button = AddButton(RulesRoot, RecipeActionButtonVariant.Craft);
            var pressCount = 0;
            using var subscription = button.OnClicked.Subscribe(_ => pressCount++);

            simulate.Click(button.worldBound.center);
            simulate.KeyPress(KeyCode.Space);
            simulate.KeyPress(KeyCode.Return);
            Assert.AreEqual(1, pressCount);
            Assert.IsNull(panel.focusController.focusedElement);

            button.SetEnabled(false);
            simulate.FrameUpdate();
            simulate.Click(button.worldBound.center);
            Assert.AreEqual(1, pressCount);
            Assert.AreEqual(TestId, button.name);
            Assert.AreEqual(Localize.Get(LocalizationKeys.Ui.Recipe.CraftButtonLabel), Label(button).text);
        }

        [Test]
        public void 既定はクラフトの寸法と字と上下左右1pxの文字の影になる()
        {
            // 幅 80.433・高 19・padding 0・12px・字間 0.12em（12）・文字 #fff・縁なし・500 の字形
            // Width 80.433, height 19, padding 0, 12px, spacing 0.12em (12), #fff text, no outline, the 500 face
            var button = AddButton(RulesRoot, RecipeActionButtonVariant.Craft);
            var label = Label(button);
            Assert.AreEqual(ResolvePixels("--recipe-craft-button-width"), button.resolvedStyle.width, 0.5f);
            Assert.AreEqual(ResolvePixels("--recipe-action-button-height"), button.resolvedStyle.height, 0.5f);
            Assert.AreEqual(0f, button.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(12f, label.resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(12f, label.resolvedStyle.letterSpacing, 0.01f);
            Assert.AreEqual(0f, label.resolvedStyle.unityTextOutlineWidth, 0.01f);
            AssertColor(Hex("#FFFFFF"), label.resolvedStyle.color, "text");
            Assert.AreEqual("GameUiLatinMedium SDF", label.resolvedStyle.unityFontDefinition.fontAsset.name);
            Assert.AreEqual(0f, button.resolvedStyle.backgroundColor.a, 0.001f);

            // 影は同じ文・同じ字形の 4 枚を右・左・下・上へ 1px ずらし、rgb(0 40 80 / 55%) で文言の下に敷く（Web の text-shadow 4 枚）
            // The shadow is four copies of the same text and face shifted 1px right, left, down and up in rgb(0 40 80 / 55%) under the label (Web four text-shadows)
            var offset = ResolvePixels("--recipe-action-text-shadow-offset");
            var expected = new[] { new Vector2(offset, 0f), new Vector2(-offset, 0f), new Vector2(0f, offset), new Vector2(0f, -offset) };
            var glyphs = ShadowGlyphs(button).ToList();
            Assert.AreEqual(4, glyphs.Count);
            Assert.Less(button.IndexOf(glyphs[0].parent), button.IndexOf(label), "shadow sits under the label");
            Assert.AreEqual(DisplayStyle.Flex, glyphs[0].parent.resolvedStyle.display);
            for (var i = 0; i < glyphs.Count; i++)
            {
                Assert.AreEqual(expected[i].x, glyphs[i].resolvedStyle.translate.x, 0.01f, $"shadow {i} x");
                Assert.AreEqual(expected[i].y, glyphs[i].resolvedStyle.translate.y, 0.01f, $"shadow {i} y");
                AssertSameColor(ResolveColor("--recipe-action-text-shadow"), glyphs[i].resolvedStyle.color, $"shadow {i} color");
                Assert.AreEqual(label.text, glyphs[i].text);
                Assert.AreEqual(12f, glyphs[i].resolvedStyle.fontSize, 0.01f);
                Assert.AreEqual(12f, glyphs[i].resolvedStyle.letterSpacing, 0.01f);
                Assert.AreEqual("GameUiLatinMedium SDF", glyphs[i].resolvedStyle.unityFontDefinition.fontAsset.name);
            }
        }

        [Test]
        public void 面の3色は通常とホバーと無効でつまみが切り替わり無効の文字は薄く不透明のまま()
        {
            var button = AddButton(RulesRoot, RecipeActionButtonVariant.Craft);
            AssertFace(button, "--recipe-action-background-start", "--recipe-action-background-middle", "--recipe-action-background-end");

            // ホバーは Web の .craftButton:hover の 3 色
            // Hover is the three Web .craftButton:hover colors
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            AssertFace(button, "--recipe-action-hover-start", "--recipe-action-hover-middle", "--recipe-action-hover-end");

            // 無効はホバーしたままでも通常のグラデ・文字は rgb(255 255 255 / 80%)・不透明
            // Disabled keeps the normal gradient even under hover, with rgb(255 255 255 / 80%) text, fully opaque
            button.SetEnabled(false);
            simulate.MouseMove(button.worldBound.center + Vector2.one);
            simulate.FrameUpdate();
            AssertFace(button, "--recipe-action-background-start", "--recipe-action-background-middle", "--recipe-action-background-end");
            AssertSameColor(ResolveColor("--recipe-action-disabled-text"), Label(button).resolvedStyle.color, "disabled text");
            Assert.AreEqual(1f, button.resolvedStyle.opacity, 0.001f);
            Assert.AreEqual(1f, Label(button).resolvedStyle.opacity, 0.001f);
        }

        [Test]
        public void 押している間は1px沈み離すと戻る()
        {
            var button = AddButton(RulesRoot, RecipeActionButtonVariant.Craft);
            simulate.MouseDown(button.worldBound.center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            Assert.AreEqual(1f, button.resolvedStyle.translate.y, 0.01f);
            simulate.MouseUp(button.worldBound.center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, button.resolvedStyle.translate.y, 0.01f);
        }

        [Test]
        public void 描画でエラーを出さない()
        {
            var button = AddButton(RulesRoot, RecipeActionButtonVariant.Craft);
            simulate.FrameUpdate();
            button.SetEnabled(false);
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }
    }
}
