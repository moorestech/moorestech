using System;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Source;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Recipe.Source
{
    public class MachineRecipeSummaryConversionTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 出力とブロックのGuidを要約へ写す()
        {
            var blockId = new BlockId(1);
            var recipe = new MachineRecipeData(Guid.NewGuid(), blockId, 2, Array.Empty<ItemAmount>(), new[] { new ItemAmount(new ItemId(3), 4) }, Array.Empty<RecipeFluidAmount>(), new[] { new RecipeFluidAmount(new FluidId(1), 5) }, RecipeLevelInheritance.None);

            var summary = MachineRecipeSummaryConversion.ToSummaries(new[] { recipe })[0];

            Assert.AreEqual(recipe.RecipeGuid, summary.RecipeGuid);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid, summary.BlockGuid);
            Assert.AreEqual(new ItemId(3), summary.OutputItems[0].ItemId);
            Assert.AreEqual(4, summary.OutputItems[0].Count);
            Assert.AreEqual(5, summary.OutputFluids[0].Amount);
        }

        [Test]
        public void 空の列は空の列()
        {
            Assert.AreEqual(0, MachineRecipeSummaryConversion.ToSummaries(Array.Empty<MachineRecipeData>()).Count);
        }
    }
}
