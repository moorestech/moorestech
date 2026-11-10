using Client.Ui.Components.Recipe;
using NUnit.Framework;

namespace Client.Tests.Ui.Components.Recipe
{
    /// <summary>
    /// スロット列数の式の L0
    /// L0 of the slot column-count formula
    /// </summary>
    public class RecipeSlotLayoutTest
    {
        /// <web-test file="src/features/recipe/logic/recipeSlotLayout.test.ts">列数は2で頭打ちになり、3点以上は行が増える</web-test>
        [Test]
        public void 列数は2で頭打ちになり3点以上は行が増える()
        {
            Assert.AreEqual(2, RecipeSlotLayout.MaxSlotColumns);
            Assert.AreEqual(1, RecipeSlotLayout.Columns(1));
            Assert.AreEqual(2, RecipeSlotLayout.Columns(2));
            Assert.AreEqual(2, RecipeSlotLayout.Columns(3));
            Assert.AreEqual(2, RecipeSlotLayout.Columns(6));
        }

        /// <web-test file="src/features/recipe/logic/recipeSlotLayout.test.ts">0点でも1列へ落として不正CSSを出さない</web-test>
        [Test]
        public void 点数0でも1列へ落とす()
        {
            Assert.AreEqual(1, RecipeSlotLayout.Columns(0));
        }
    }
}
