using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Controls;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Controls.RecipeAction
{
    /// <summary>
    /// ホバーで面が描き直される（L1）
    /// Hovering repaints the face (L1)
    /// </summary>
    public class RecipeActionButtonHoverRepaintTest : UiTestHost
    {
        [Test]
        public void ホバーで面が描き直され最後に描いた色がホバーの3色になる()
        {
            Localize.Initialize();
            var button = new RecipeActionButton("recipe-action-hover-test", new UiText(LocalizationKeys.Ui.Recipe.CraftButtonLabel), RecipeActionButtonVariant.Craft);
            rootVisualElement.Add(button);
            simulate.FrameUpdate();
            var face = button.Q<RecipeActionButtonFace>(className: RecipeActionButtonFace.UssClassName);
            AssertSameColor(ResolveColor("--recipe-action-background-start"), face.LastDrawnStartForTest, "normal start");
            var drawsBeforeHover = face.DrawCountForTest;

            // 本番と同じポインタの移動で :hover を付け、面が描いた色を読む
            // Apply :hover through the same pointer move as production and read the colors the face drew
            simulate.MouseMove(button.worldBound.center);
            simulate.FrameUpdate();
            Assert.Greater(face.DrawCountForTest, drawsBeforeHover, "face repainted on hover");
            AssertSameColor(ResolveColor("--recipe-action-hover-start"), face.LastDrawnStartForTest, "hover start");
            AssertSameColor(ResolveColor("--recipe-action-hover-middle"), face.LastDrawnMiddleForTest, "hover middle");
            AssertSameColor(ResolveColor("--recipe-action-hover-end"), face.LastDrawnEndForTest, "hover end");
            Assert.AreNotEqual(ResolveColor("--recipe-action-background-start"), face.LastDrawnStartForTest, "hover differs from normal");
        }
    }
}
