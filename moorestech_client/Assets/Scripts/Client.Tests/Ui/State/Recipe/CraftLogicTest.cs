using System.Collections.Generic;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Logic;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.Recipe.RecipeFixtures;

namespace Client.Tests.Ui.State.Recipe
{
    public class CraftLogicTest
    {
        private static readonly MachineRecipeData[] NoMachines = new MachineRecipeData[0];
        private static readonly CraftRecipeData[] NoCrafts = new CraftRecipeData[0];
        private static readonly CraftRecipeData[] CraftOnly = { Craft(RecipeGuid(1), 9, 1, 1), Craft(RecipeGuid(2), 5, 1, 1), Craft(RecipeGuid(3), 9, 1, 1) };

        private static Dictionary<ItemId, int> Counts(params (int itemId, int count)[] pairs)
        {
            var counts = new Dictionary<ItemId, int>();
            foreach (var (itemId, count) in pairs) counts[new ItemId(itemId)] = count;
            return counts;
        }

        private static void AssertGuids(IReadOnlyList<RecipeEntry> entries, params System.Guid[] expected)
        {
            Assert.AreEqual(expected.Length, entries.Count);
            for (var i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], entries[i].RecipeGuid);
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">素材の最小商に完成個数を掛け、同じ完成品は最大値を採用する</web-test>
        [Test]
        public void 素材の最小商に完成個数を掛け同じ完成品は最大値を採る()
        {
            var recipes = new[] { Craft(RecipeGuid(1), 9, 2, 1, Amount(1, 3)), Craft(RecipeGuid(2), 9, 1, 1, Amount(2, 2)), Craft(RecipeGuid(3), 8, 1, 1, Amount(3, 1)) };
            var result = CraftLogic.CraftableResultCounts(recipes, Counts((1, 7), (2, 10)));
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(5, result[new ItemId(9)]);
        }

        [Test]
        public void 必要数0の素材はゼロ除算せず所持があれば読み飛ばし所持0なら作れない()
        {
            var recipes = new[] { Craft(RecipeGuid(1), 9, 1, 1, Amount(1, 0), Amount(2, 2)) };
            Assert.AreEqual(5, CraftLogic.CraftableResultCounts(recipes, Counts((1, 1), (2, 10)))[new ItemId(9)]);
            Assert.AreEqual(0, CraftLogic.CraftableResultCounts(recipes, Counts((2, 10))).Count);
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">全素材を満たせば true</web-test>
        [Test]
        public void 全素材を満たせば作れる()
        {
            var recipe = Craft(RecipeGuid(1), 9, 1, 1, Amount(1, 2), Amount(2, 1));
            Assert.IsTrue(CraftLogic.Craftable(recipe, Counts((1, 2), (2, 1))));
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">一つでも不足なら false</web-test>
        [Test]
        public void 一つでも不足なら作れない()
        {
            var recipe = Craft(RecipeGuid(1), 9, 1, 1, Amount(1, 2), Amount(2, 1));
            Assert.IsFalse(CraftLogic.Craftable(recipe, Counts((1, 1), (2, 1))));
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">クラフトレシピを先頭に、機械レシピを後ろにデータ順で並べる</web-test>
        [Test]
        public void クラフトを先頭に機械を後ろにデータ順で並べる()
        {
            var crafts = new[] { Craft(RecipeGuid(101), 9, 1, 2, Amount(1, 1)), Craft(RecipeGuid(102), 5, 1, 2, Amount(1, 1)), Craft(RecipeGuid(103), 9, 1, 2, Amount(1, 1)) };
            var none = RecipeLevelInheritance.None();
            var machines = new[] { Machine(RecipeGuid(201), 9, 100, none), Machine(RecipeGuid(202), 7, 100, none), Machine(RecipeGuid(203), 9, 200, none) };
            var entries = CraftLogic.BuildRecipeEntries(crafts, machines, new ItemId(9));
            AssertGuids(entries, RecipeGuid(101), RecipeGuid(103), RecipeGuid(201), RecipeGuid(203));
            Assert.IsInstanceOf<CraftRecipeEntry>(entries[0]);
            Assert.IsInstanceOf<CraftRecipeEntry>(entries[1]);
            Assert.IsInstanceOf<MachineRecipeEntry>(entries[2]);
            Assert.IsInstanceOf<MachineRecipeEntry>(entries[3]);
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">対象アイテムのレシピが無ければ空配列</web-test>
        [Test]
        public void 対象アイテムのレシピが無ければ空()
        {
            CollectionAssert.IsEmpty(CraftLogic.BuildRecipeEntries(NoCrafts, NoMachines, new ItemId(9)));
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">クラフトはresultItemId一致のみ抽出する</web-test>
        [Test]
        public void クラフトは完成品が一致するものだけを抽出する()
        {
            var entries = CraftLogic.BuildRecipeEntries(CraftOnly, NoMachines, new ItemId(9));
            AssertGuids(entries, RecipeGuid(1), RecipeGuid(3));
            foreach (var entry in entries) Assert.IsInstanceOf<CraftRecipeEntry>(entry);
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">クラフトのresultItemIdが一致しなければ空配列</web-test>
        [Test]
        public void クラフトの完成品が一致しなければ空()
        {
            CollectionAssert.IsEmpty(CraftLogic.BuildRecipeEntries(CraftOnly, NoMachines, new ItemId(42)));
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">継承レシピは上位レベルの製品からも引ける</web-test>
        [Test]
        public void 継承レシピは上位レベルの製品からも引ける()
        {
            var inherit = RecipeLevelInheritance.Inherit(0, Ids(1, 2, 3), 0, Ids(9, 19, 29), 3);
            var entries = CraftLogic.BuildRecipeEntries(NoCrafts, new[] { Machine(RecipeGuid(209), 9, 100, inherit) }, new ItemId(19));
            AssertGuids(entries, RecipeGuid(209));
        }

        /// <web-test file="src/features/recipe/logic/craftLogic.test.ts">継承しないレシピは継承先の並びを読まない</web-test>
        [Test]
        public void 継承しないレシピは継承先から引けない()
        {
            var machines = new[] { Machine(RecipeGuid(208), 8, 100, RecipeLevelInheritance.None()) };
            CollectionAssert.IsEmpty(CraftLogic.BuildRecipeEntries(NoCrafts, machines, new ItemId(19)));
        }
    }
}
