using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using static Client.Tests.Ui.State.Recipe.RecipeFixtures;

namespace Client.Tests.Ui.State.Recipe
{
    public class LevelInheritanceBadgeTextTest
    {
        /// <web-test file="src/features/recipe/logic/levelInheritanceBadge.test.ts">継承しないレシピにはバッジを出さない</web-test>
        [Test]
        public void 継承しないレシピにはバッジを出さない()
        {
            Assert.IsFalse(LevelInheritanceBadgeText.TryBuild(RecipeLevelInheritance.None, out _));
        }

        /// <web-test file="src/features/recipe/logic/levelInheritanceBadge.test.ts">上限なしの継承は LvN→LvN</web-test>
        [Test]
        public void 上限なしの継承は差し込み無しの文になる()
        {
            var inherit = RecipeLevelInheritance.Inherit(0, Ids(11, 12, 13), 0, Ids(21, 22, 23), 3);
            Assert.IsTrue(LevelInheritanceBadgeText.TryBuild(inherit, out var text));
            Assert.AreEqual(LocalizationKeys.Ui.Recipe.LevelInheritance.Key, text.Key.Key);
            Assert.AreEqual(0, text.Params.Count);
        }

        /// <web-test file="src/features/recipe/logic/levelInheritanceBadge.test.ts">上限つきの継承は今の上限を差し込む</web-test>
        [Test]
        public void 上限つきの継承は今の上限を差し込む()
        {
            var capped = RecipeLevelInheritance.InheritUpToUnlocked(0, Ids(11, 12, 13), 0, Ids(21, 22, 23), 2);
            Assert.IsTrue(LevelInheritanceBadgeText.TryBuild(capped, out var text));
            Assert.AreEqual(LocalizationKeys.Ui.Recipe.LevelInheritanceCapped.Key, text.Key.Key);
            Assert.AreEqual(1, text.Params.Count);
            Assert.AreEqual("level", text.Params[0].Name);
            Assert.AreEqual("2", text.Params[0].Value);
        }
    }
}
