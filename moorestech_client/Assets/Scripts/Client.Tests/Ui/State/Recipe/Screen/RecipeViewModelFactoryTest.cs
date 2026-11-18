using System.Collections.Generic;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Screen;
using Core.Master;
using NUnit.Framework;
using static Client.Tests.Ui.State.Recipe.RecipeFixtures;

namespace Client.Tests.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 選択・データ・持ち数から 2 つの VM を組む（L0）
    /// Builds the two VMs from selection, data and owned counts (L0)
    /// </summary>
    public class RecipeViewModelFactoryTest
    {
        private static readonly Dictionary<ItemId, int> Owned = new() { { new ItemId(1), 3 } };

        /// <web-test file="src/features/recipe/views/RecipeContent.test.ts">全レシピをクラフト優先の単一リストへ並べる</web-test>
        [Test]
        public void 全レシピをクラフト優先の単一リストへ並べる()
        {
            var crafts = new[] { Craft(RecipeGuid(1), 7, 1, 2, Amount(1, 2)) };
            var machines = new[] { Machine(RecipeGuid(2), 7, 5, RecipeLevelInheritance.None) };

            var vm = (RecipeViewerItemSelected)RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), crafts, machines, Owned);

            Assert.AreEqual(2, vm.Entries.Count);
            var craft = (CraftRecipeEntryViewModel)vm.Entries[0];
            Assert.IsInstanceOf<MachineRecipeEntryViewModel>(vm.Entries[1]);
            Assert.IsTrue(craft.Craftable);
            Assert.AreEqual(3, craft.Materials[0].Owned);
            Assert.IsFalse(craft.Materials[0].Lacking);
        }

        /// <web-test file="src/features/recipe/views/RecipeContent.test.ts">チュートリアルアンカーを先頭のクラフトエントリ1件だけへ付ける</web-test>
        [Test]
        public void チュートリアルアンカーを先頭のクラフトエントリ1件だけへ付ける()
        {
            var crafts = new[] { Craft(RecipeGuid(1), 7, 1, 2, Amount(1, 9)), Craft(RecipeGuid(3), 7, 1, 0.4, Amount(1, 1)) };

            var vm = (RecipeViewerItemSelected)RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), crafts, new MachineRecipeData[0], Owned);

            Assert.IsTrue(((CraftRecipeEntryViewModel)vm.Entries[0]).CarriesTutorialAnchor);
            Assert.IsFalse(((CraftRecipeEntryViewModel)vm.Entries[1]).CarriesTutorialAnchor);
            Assert.IsTrue(((CraftRecipeEntryViewModel)vm.Entries[0]).Materials[0].Lacking);
        }

        [Test]
        public void 機械レシピだけならアンカーを付けず未選択は案内のVMになる()
        {
            var machines = new[] { Machine(RecipeGuid(2), 7, 5, RecipeLevelInheritance.None) };
            var vm = (RecipeViewerItemSelected)RecipeViewModelFactory.BuildViewer(RecipeSelection.Of(new ItemId(7)), new CraftRecipeData[0], machines, Owned);
            Assert.IsInstanceOf<MachineRecipeEntryViewModel>(vm.Entries[0]);
            Assert.AreSame(RecipeViewerNoSelection.Instance, RecipeViewModelFactory.BuildViewer(RecipeSelection.None, new CraftRecipeData[0], machines, Owned));
        }

        [Test]
        public void 一覧のセルはアイテムの順でクラフト可能数を持ち作れないものは0()
        {
            var crafts = new[] { Craft(RecipeGuid(1), 7, 2, 2, Amount(1, 1)) };
            var list = RecipeViewModelFactory.BuildItemList(Ids(7, 8), crafts, Owned);
            Assert.AreEqual(new ItemId(7), list.Cells[0].ItemId);
            Assert.AreEqual(6, list.Cells[0].CraftableCount);
            Assert.AreEqual(0, list.Cells[1].CraftableCount);
        }
    }
}
