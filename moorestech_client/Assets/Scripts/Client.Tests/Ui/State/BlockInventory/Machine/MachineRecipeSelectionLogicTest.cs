using System;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Core.Master;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// Web の machineRecipeSelectionLogic.test.ts の写し（L0）
    /// Port of Web machineRecipeSelectionLogic.test.ts (L0)
    /// </summary>
    public class MachineRecipeSelectionLogicTest
    {
        private static readonly Guid BlockA = new Guid("85000000-0000-4000-8000-000000000001");
        private static readonly Guid BlockB = new Guid("85000000-0000-4000-8000-000000000002");
        private static readonly Guid Recipe1 = new Guid("84000000-0000-4000-8000-000000000001");
        private static readonly Guid Recipe2 = new Guid("84000000-0000-4000-8000-000000000002");

        /// <web-test file="src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts">開いている機械のレシピだけを選択フラグ付きで返す</web-test>
        [Test]
        public void 開いている機械のレシピだけを選択フラグ付きで返す()
        {
            var a = Recipe(Recipe1, BlockA, new[] { new RecipeOutputItem(new ItemId(2), 1) }, new RecipeOutputFluid[0]);
            var b = Recipe(Recipe2, BlockB, new[] { new RecipeOutputItem(new ItemId(2), 1) }, new RecipeOutputFluid[0]);
            var rows = MachineRecipeSelectionLogic.BuildRows(new[] { a, b }, BlockA, Recipe1);
            Assert.AreEqual(1, rows.Count);
            Assert.AreSame(a, rows[0].Recipe);
            var subject = (ItemRecipeDisplaySubject)rows[0].Subject;
            Assert.AreEqual(new ItemId(2), subject.ItemId);
            Assert.AreEqual(1, subject.Count);
            Assert.IsTrue(rows[0].Selected);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts">空GUIDはどの行も選択しない</web-test>
        [Test]
        public void 空GUIDはどの行も選択しない()
        {
            var a = Recipe(Recipe1, BlockA, new[] { new RecipeOutputItem(new ItemId(2), 1) }, new RecipeOutputFluid[0]);
            Assert.IsFalse(MachineRecipeSelectionLogic.BuildRows(new[] { a }, BlockA, Guid.Empty)[0].Selected);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts">代表出力（アイテムも液体も）が無いレシピは除外する</web-test>
        [Test]
        public void 代表出力の無いレシピは除外する()
        {
            var noOutput = Recipe(new Guid("84000000-0000-4000-8000-000000000003"), BlockA, new RecipeOutputItem[0], new RecipeOutputFluid[0]);
            CollectionAssert.IsEmpty(MachineRecipeSelectionLogic.BuildRows(new[] { noOutput }, BlockA, Guid.Empty));
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts">出力アイテムが無く出力液体のみのレシピは液体を代表にして残す</web-test>
        [Test]
        public void 出力が液体だけのレシピは液体を代表にして残す()
        {
            var fluid = new FluidId(7);
            var fluidOnly = Recipe(new Guid("84000000-0000-4000-8000-000000000004"), BlockA, new RecipeOutputItem[0], new[] { new RecipeOutputFluid(fluid, 100) });
            var rows = MachineRecipeSelectionLogic.BuildRows(new[] { fluidOnly }, BlockA, Guid.Empty);
            Assert.AreEqual(1, rows.Count);
            var subject = (FluidRecipeDisplaySubject)rows[0].Subject;
            Assert.AreEqual(fluid, subject.FluidId);
            Assert.AreEqual(100, subject.Amount);
            Assert.IsFalse(rows[0].Selected);
        }

        /// <web-test file="src/features/blockInventory/details/machine/machineRecipeSelectionLogic.test.ts">一致するレシピがなければ空配列を返す</web-test>
        [Test]
        public void 一致するレシピが無ければ空を返す()
        {
            var b = Recipe(Recipe1, BlockB, new[] { new RecipeOutputItem(new ItemId(2), 1) }, new RecipeOutputFluid[0]);
            CollectionAssert.IsEmpty(MachineRecipeSelectionLogic.BuildRows(new[] { b }, BlockA, Guid.Empty));
        }

        private static MachineRecipeSummary Recipe(Guid recipeGuid, Guid blockGuid, RecipeOutputItem[] outputItems, RecipeOutputFluid[] outputFluids)
        {
            return new MachineRecipeSummary(recipeGuid, blockGuid, outputItems, outputFluids);
        }
    }
}
