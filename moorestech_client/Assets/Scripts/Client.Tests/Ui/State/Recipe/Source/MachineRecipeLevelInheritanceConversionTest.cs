using Client.Ui.State.Recipe.Data;
using Client.Ui.State.Recipe.Source;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Recipe.Source
{
    /// <summary>
    /// マスタの継承レシピを、研究で上がる上限つき・全長まで・継承なしの 3 形へ写すことを確かめる（L0）
    /// Verifies master inheritance recipes map to capped-by-research, full-length and none (L0)
    /// </summary>
    public class MachineRecipeLevelInheritanceConversionTest
    {
        private ServiceProvider _serviceProvider;

        [SetUp]
        public void SetUp()
        {
            (_, _serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 上限つきの継承は研究で最高レベルが上がる()
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid);
            var craftCapLookup = _serviceProvider.GetService<ILevelInheritanceCraftCapLookup>();

            // 未研究の上限つきレシピは Lv1 まで
            // An unresearched capped recipe accepts up to Lv1
            var before = (LevelInheritanceMapping)MachineRecipeLevelInheritanceConversion.Convert(recipe, craftCapLookup);
            Assert.IsTrue(before.CappedAtUnlockedLevel);
            Assert.AreEqual(0, before.SourceInputIndex);
            Assert.AreEqual(0, before.TargetOutputIndex);
            CollectionAssert.AreEqual(new[] { ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3) }, before.SourceItemIds);
            CollectionAssert.AreEqual(new[] { ForUnitTestLevelInheritanceIds.CappedTarget(1), ForUnitTestLevelInheritanceIds.CappedTarget(2), ForUnitTestLevelInheritanceIds.CappedTarget(3) }, before.TargetItemIds);
            Assert.AreEqual(1, before.MaxCraftableLevel);

            // 上限2で最高Lvも2
            // Cap 2 gives max level 2
            _serviceProvider.GetService<ILevelInheritanceCraftCapUnlocker>().ApplyUnlockLevelInheritanceCraftLevelAction(MasterHolder.ResearchMaster.GetResearch(ForUnitTestLevelInheritanceIds.CraftCapResearchGuid).ClearedActions.items[0]);
            Assert.AreEqual(2, ((LevelInheritanceMapping)MachineRecipeLevelInheritanceConversion.Convert(recipe, craftCapLookup)).MaxCraftableLevel);
        }

        [Test]
        public void 上限なしの継承は全長まで受け継がないレシピは継承なし()
        {
            var craftCapLookup = _serviceProvider.GetService<ILevelInheritanceCraftCapLookup>();

            var inherit = (LevelInheritanceMapping)MachineRecipeLevelInheritanceConversion.Convert(MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.InheritRecipeGuid), craftCapLookup);
            Assert.IsFalse(inherit.CappedAtUnlockedLevel);
            Assert.AreEqual(3, inherit.MaxCraftableLevel);

            Assert.AreSame(RecipeLevelInheritance.None, MachineRecipeLevelInheritanceConversion.Convert(MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0], craftCapLookup));
        }
    }
}
