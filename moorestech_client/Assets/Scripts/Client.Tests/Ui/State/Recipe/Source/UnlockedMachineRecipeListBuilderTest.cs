using System;
using System.Collections.Generic;
using System.Linq;
using Client.Ui.State.Recipe.Source;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Client.Tests.Ui.State.Recipe.Source
{
    /// <summary>
    /// 解放済みの機械レシピだけを Web の MachineRecipesTopic と同じ形で並べる（L0）
    /// Lists only unlocked machine recipes in the same shape as the Web MachineRecipesTopic (L0)
    /// </summary>
    public class UnlockedMachineRecipeListBuilderTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 解放済みのレシピだけを並べ解放表に無いレシピは出さない()
        {
            var all = MasterHolder.MachineRecipesMaster.MachineRecipes.Data;
            Assert.GreaterOrEqual(all.Length, 2, "テスト用 mod に機械レシピが 2 件以上ある前提");
            var unlock = new FakeGameUnlockStateData();
            unlock.SetMachineRecipe(all[0].MachineRecipeGuid, true);
            unlock.SetMachineRecipe(all[1].MachineRecipeGuid, false);

            var recipes = UnlockedMachineRecipeListBuilder.Build(unlock, new FixedCraftCapLookup());

            Assert.AreEqual(new[] { all[0].MachineRecipeGuid }, recipes.Select(r => r.RecipeGuid).ToArray());
        }

        [Test]
        public void 入出力と時間とブロックをマスタから写す()
        {
            var master = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.OutputItems.Length > 0);
            var unlock = new FakeGameUnlockStateData();
            unlock.SetMachineRecipe(master.MachineRecipeGuid, true);

            var recipe = UnlockedMachineRecipeListBuilder.Build(unlock, new FixedCraftCapLookup()).Single();

            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockId(master.BlockGuid), recipe.BlockId);
            Assert.AreEqual(master.Time, recipe.Time);
            Assert.AreEqual(master.InputItems.Length, recipe.InputItems.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(master.OutputItems[0].ItemGuid), recipe.OutputItems[0].ItemId);
            Assert.AreEqual(master.OutputItems[0].Count, recipe.OutputItems[0].Count);
            Assert.AreEqual(master.InputFluids.Length, recipe.InputFluids.Count);
        }
    }

    internal sealed class FixedCraftCapLookup : ILevelInheritanceCraftCapLookup
    {
        public IReadOnlyDictionary<Guid, int> UnlockedCaps { get; } = new Dictionary<Guid, int>();
        public IObservable<(Guid familyBaseItemGuid, int level)> OnCraftCapUnlocked { get; } = new Subject<(Guid, int)>();

        public int GetCraftCap(Guid familyBaseItemGuid)
        {
            return 1;
        }
    }
}
