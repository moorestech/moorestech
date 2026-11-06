using Client.Ui.State.Recipe.Logic;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Recipe
{
    public class RecipeSlotLayoutTest
    {
        // Web のトークン値（--recipe-slot-size-max 2.3rem＝36.8px、--recipe-slot-gap 10px）
        // Web token values (--recipe-slot-size-max 2.3rem = 36.8px, --recipe-slot-gap 10px)
        private const float SlotSizeMax = 36.8f;
        private const float SlotGap = 10f;

        /// <web-test file="src/features/recipe/logic/recipeSlotLayout.test.ts">列数は2で頭打ちになり、3点以上は行が増える</web-test>
        [Test]
        public void 列数は2で頭打ちになり3点以上は行が増える()
        {
            Assert.AreEqual(2, RecipeSlotLayout.MaxSlotColumns);
            Assert.AreEqual(1, RecipeSlotLayout.Compute(1, 100, SlotSizeMax, SlotGap).Columns);
            Assert.AreEqual(2, RecipeSlotLayout.Compute(2, 100, SlotSizeMax, SlotGap).Columns);
            Assert.AreEqual(2, RecipeSlotLayout.Compute(3, 100, SlotSizeMax, SlotGap).Columns);
            Assert.AreEqual(2, RecipeSlotLayout.Compute(6, 100, SlotSizeMax, SlotGap).Columns);
        }

        /// <web-test file="src/features/recipe/logic/recipeSlotLayout.test.ts">0点でも1列へ落として不正CSSを出さない</web-test>
        [Test]
        public void 点数0でも1列へ落としてゼロ除算しない()
        {
            var layout = RecipeSlotLayout.Compute(0, 100, SlotSizeMax, SlotGap);
            Assert.AreEqual(1, layout.Columns);
            Assert.IsFalse(float.IsNaN(layout.SlotSize) || float.IsInfinity(layout.SlotSize));
            Assert.AreEqual(SlotSizeMax, layout.SlotSize, 1e-4f);
        }

        /// <web-test file="src/features/recipe/logic/recipeSlotLayout.test.ts">スロット寸法は列数ぶんのgapを差し引いて上限で頭打ちになる</web-test>
        [Test]
        public void スロット寸法は列数ぶんのgapを引いて上限で頭打ちになる()
        {
            Assert.AreEqual(30f, RecipeSlotLayout.Compute(1, 30, 100, SlotGap).SlotSize, 1e-4f);
            Assert.AreEqual(20f, RecipeSlotLayout.Compute(4, 50, 100, SlotGap).SlotSize, 1e-4f);
            Assert.AreEqual(SlotSizeMax, RecipeSlotLayout.Compute(4, 1000, SlotSizeMax, SlotGap).SlotSize, 1e-4f);
        }
    }
}
