using Client.Ui.Components.Controls;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ComponentStyleAssert;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Controls.RecipeAction
{
    /// <summary>
    /// RecipeActionButton の文言のラベル・影の字・面の 3 色を読んで照合する補助（L1）
    /// Helpers that read the RecipeActionButton label and shadow copies and check the three face colors (L1)
    /// </summary>
    public static class RecipeActionButtonAssert
    {
        public static Label Label(RecipeActionButton button)
        {
            return button.Q<Label>(className: RecipeActionButton.LabelUssClassName);
        }

        public static UQueryState<Label> ShadowGlyphs(RecipeActionButton button)
        {
            return button.Query<Label>(className: RecipeActionLabelShadow.GlyphUssClassName).Build();
        }

        public static void AssertFace(RecipeActionButton button, string startToken, string middleToken, string endToken)
        {
            // 描く子の customStyle に受け直した 3 色を、トークンの値と照合する
            // Compare the three colors re-declared on the drawing child's customStyle with the token values
            var face = button.Q(className: "recipe-action-button__face");
            AssertSameColor(ResolveColor(startToken), FaceColor(face, RecipeActionButton.FaceStartProperty), "face start");
            AssertSameColor(ResolveColor(middleToken), FaceColor(face, RecipeActionButton.FaceMiddleProperty), "face middle");
            AssertSameColor(ResolveColor(endToken), FaceColor(face, RecipeActionButton.FaceEndProperty), "face end");
        }

        private static Color FaceColor(VisualElement face, string property)
        {
            Assert.IsTrue(face.customStyle.TryGetValue(new CustomStyleProperty<Color>(property), out var color), property);
            return color;
        }
    }
}
