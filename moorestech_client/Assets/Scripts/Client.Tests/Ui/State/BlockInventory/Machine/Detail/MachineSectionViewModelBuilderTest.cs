using System;
using System.Linq;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.BlockInventory.Machine.Detail
{
    /// <summary>
    /// 機械の区画の値の組み立て（行の絞り込み・選択行・帯・ゴースト・レシピの引き当て・行の覚え D-39）
    /// Building the machine section's values (row filtering, selected row, bands, ghosts, recipe lookup, row cache D-39)
    /// </summary>
    public class MachineSectionViewModelBuilderTest
    {
        private static readonly Guid RecipeA = MachineTestDetails.RecipeA;
        private static readonly Guid RecipeB = MachineTestDetails.RecipeB;
        private static readonly Guid OtherBlockRecipe = new("00000000-0000-0000-0000-00000000c00c");

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 行は開いた機械のレシピだけで選択中の行が取れる()
        {
            // 別ブロックのレシピは行にしない
            // Recipes of another block never become rows
            var machineId = MasterHolder.BlockMaster.GetBlockId(MachineTestDetails.MachineBlock.BlockGuid);
            var otherId = MasterHolder.BlockMaster.Blocks.Data.Select(b => MasterHolder.BlockMaster.GetBlockId(b.BlockGuid)).First(id => id != machineId);
            var catalog = MachineRecipeCatalog.Build(new[] { Recipe(RecipeA, machineId), Recipe(OtherBlockRecipe, otherId), Recipe(RecipeB, machineId) });
            var detail = MachineTestDetails.Detail(MachineRecipeSelection.Selected(RecipeB), MachineTestDetails.Unbound(new MachineSlotLayout(1, 1, 0)), 0);

            var viewModel = MachineTestDetails.ViewModelFor(MachineTestDetails.DefaultInstanceId, detail, 2, catalog);

            CollectionAssert.AreEqual(new[] { RecipeA, RecipeB }, viewModel.Rows.Select(r => r.Recipe.RecipeGuid).ToArray());
            Assert.IsTrue(viewModel.TryGetSelectedRow(out var selected));
            Assert.AreEqual(RecipeB, selected.Recipe.RecipeGuid);
            Assert.IsFalse(MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected, RecipeA).TryGetSelectedRow(out _));
        }

        [Test]
        public void 帯は構成で枠数を割りゴーストは束縛から出る()
        {
            var layout = new MachineSlotLayout(2, 1, 1);
            var binding = new MachineBindingDetail(layout, new[] { new MachineItemSlotBinding(1, new ItemId(1), 3, new[] { new ItemId(1) }) }, Array.Empty<MachineTankBinding>());
            var detail = MachineTestDetails.Detail(MachineRecipeSelection.Selected(RecipeA), binding, 0);

            var viewModel = MachineTestDetails.ViewModelFor(MachineTestDetails.DefaultInstanceId, detail, 4, MachineTestDetails.Catalog(RecipeA));

            var expected = layout.Split(4);
            CollectionAssert.AreEqual(expected.Input, viewModel.Bands.Input);
            CollectionAssert.AreEqual(expected.Output, viewModel.Bands.Output);
            CollectionAssert.AreEqual(expected.Module, viewModel.Bands.Module);
            Assert.AreEqual(1, viewModel.SlotView.Inputs.Count);
            Assert.IsInstanceOf<GhostedItemSlot>(viewModel.SlotView.Inputs[0]);
            Assert.AreEqual(1, viewModel.SlotView.Inputs[0].Index);
            Assert.AreEqual(3, ((GhostedItemSlot)viewModel.SlotView.Inputs[0]).Ghost.Count);
        }

        [Test]
        public void 解放済みのレシピを引けて未知のGUIDは引けない()
        {
            var viewModel = MachineTestDetails.ViewModel(MachineRecipeSelection.Unselected, RecipeA);
            Assert.IsTrue(viewModel.TryGetRecipe(RecipeA, out var recipe));
            Assert.AreEqual(RecipeA, recipe.RecipeGuid);
            Assert.IsFalse(viewModel.TryGetRecipe(RecipeB, out _));
        }

        [Test]
        public void 同じカタログでブロックと選択が同じなら同じ列を返し選択が変わると組み直す()
        {
            var catalog = MachineTestDetails.Catalog(RecipeA, RecipeB);
            var blockGuid = MachineTestDetails.MachineBlock.BlockGuid;

            var first = catalog.RowsFor(blockGuid, MachineRecipeSelection.Selected(RecipeA));
            Assert.AreSame(first, catalog.RowsFor(blockGuid, MachineRecipeSelection.Selected(RecipeA)));
            var changed = catalog.RowsFor(blockGuid, MachineRecipeSelection.Selected(RecipeB));
            Assert.AreNotSame(first, changed);
            Assert.IsTrue(changed.Single(r => r.Recipe.RecipeGuid == RecipeB).Selected);
        }

        private static MachineRecipeData Recipe(Guid recipeGuid, BlockId blockId)
        {
            return new MachineRecipeData(recipeGuid, blockId, 2, Array.Empty<ItemAmount>(), new[] { new ItemAmount(new ItemId(1), 1) }, Array.Empty<RecipeFluidAmount>(), Array.Empty<RecipeFluidAmount>(), RecipeLevelInheritance.None);
        }
    }
}
