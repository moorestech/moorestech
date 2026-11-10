using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using static Client.Tests.Ui.Components.Controls.RecipeAction.RecipeActionButtonAssert;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Controls.RecipeAction
{
    /// <summary>
    /// 研究の変種の組と、画面が上書きしてよい寸法（L1・A-190）
    /// The research variant set and the size a screen may override (L1, A-190)
    /// </summary>
    public class RecipeActionResearchVariantTest : RecipeActionButtonFixture
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Controls/RecipeAction/RecipeActionKnobOverrides.uss";

        [Test]
        public void 研究の変種は祖先の上書き無しで研究の組になりホバーでは面が変わらない()
        {
            var button = AddButton(RulesRoot, RecipeActionButtonVariant.Research);
            Assert.IsTrue(button.ClassListContains(RecipeActionButton.ResearchUssClassName));
            Assert.AreEqual("GameUiLatin SDF", Label(button).resolvedStyle.unityFontDefinition.fontAsset.name, "research is the default 400 face");
            Assert.AreEqual(8f, button.resolvedStyle.paddingTop, 0.01f);
            Assert.AreEqual(0f, button.resolvedStyle.borderLeftWidth, 0.01f);
            Assert.AreEqual(14f, Label(button).resolvedStyle.fontSize, 0.01f);
            Assert.AreEqual(DisplayStyle.None, ShadowGlyphs(button).First().parent.resolvedStyle.display, "research has no text shadow");
            // 寸法は Web の素の button どおり中身から決まる（クラフトの 80px にならない）
            // Size comes from the content like the Web plain button (not the craft 80px)
            Assert.Greater(button.resolvedStyle.height, 19f + 8f, "auto height grows with the 8px padding");

            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            AssertFace(button, "--recipe-action-background-start", "--recipe-action-background-middle", "--recipe-action-background-end");
            simulate.MouseDown(button.worldBound.center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            Assert.AreEqual(0f, button.resolvedStyle.translate.y, 0.01f, "research does not sink");
            simulate.MouseUp(button.worldBound.center, MouseButton.LeftMouse);
            simulate.FrameUpdate();

            button.SetEnabled(false);
            simulate.FrameUpdate();
            AssertFace(button, "--research-action-disabled-face", "--research-action-disabled-face", "--research-action-disabled-face");
            AssertSameColor(ResolveColor("--text-default"), Label(button).resolvedStyle.color, "disabled text");
            Assert.AreEqual(float.Parse(Resolve("--research-action-disabled-opacity"), System.Globalization.CultureInfo.InvariantCulture), button.resolvedStyle.opacity, 0.001f);
        }

        [Test]
        public void 研究の寸法は画面が祖先で上書きできる()
        {
            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            var screen = new VisualElement();
            screen.styleSheets.Add(probe);
            screen.AddToClassList("recipe-action-research-size-probe");
            RulesRoot.Add(screen);
            var button = AddButton(screen, RecipeActionButtonVariant.Research);
            Assert.AreEqual(300f, button.resolvedStyle.width, 0.5f);
        }
    }
}
