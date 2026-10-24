using Game.Block.Blocks.Machine;
using Game.Context;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface.Component;
using Game.Block.Interface.MachineUpgrade;
using Game.PlayerIdentity;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.PlayerIdentity;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     上限つき継承レシピが製作上限より上の継承元を受け入れず、研究で上限が上がり、ロードで戻ることを検証する
    ///     Verifies a capped inheritance recipe refuses sources above the craft cap, research raises the cap, and loading restores it
    /// </summary>
    public class LevelInheritanceCraftCapTest
    {
        [Test]
        public void CappedRecipeRefusesSourceAboveCapTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, _) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid));
            Assert.AreEqual(1, serviceProvider.GetService<ILevelInheritanceCraftCapLookup>().GetCraftCap(ForUnitTestLevelInheritanceIds.CappedTargetBaseGuid));

            // 上限1では Lv1 だけが置け、Lv2 はプレイヤー操作でも自動搬入でも入らない
            // With cap 1 only Lv1 fits; Lv2 enters neither by player placement nor by automated insertion
            Assert.AreEqual(MachineSlotPlacementCheck.Allowed, inventory.CheckPlacement(0, Create(ForUnitTestLevelInheritanceIds.Source(1), 1)));
            Assert.AreEqual(MachineSlotPlacementCheck.LevelAboveCraftCap, inventory.CheckPlacement(0, Create(ForUnitTestLevelInheritanceIds.Source(2), 1)));
            var remainder = inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            Assert.AreEqual(2, remainder.Count);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
        }

        [Test]
        public void UncappedInheritIgnoresCraftCapTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, _) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid));

            // Inherit（上限なし）は研究前でも Lv3 を受け入れる
            // Inherit (uncapped) accepts Lv3 even before any research
            Assert.AreEqual(MachineSlotPlacementCheck.Allowed, inventory.CheckPlacement(0, Create(ForUnitTestLevelInheritanceIds.Source(3), 1)));
        }

        [Test]
        public void ResearchRaisesCraftCapTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);

            // 研究で上限2になり、Lv2 は入るが Lv3 はまだ入らない
            // The research raises the cap to 2; Lv2 fits but Lv3 still does not
            Assert.IsTrue(serviceProvider.GetService<IResearchDataStore>().CompleteResearch(ForUnitTestLevelInheritanceIds.CraftCapResearchGuid, playerId));
            Assert.AreEqual(2, serviceProvider.GetService<ILevelInheritanceCraftCapLookup>().GetCraftCap(ForUnitTestLevelInheritanceIds.CappedTargetBaseGuid));
            Assert.AreEqual(MachineSlotPlacementCheck.LevelAboveCraftCap, inventory.CheckPlacement(0, Create(ForUnitTestLevelInheritanceIds.Source(3), 1)));

            // Lv2 を回すと継承先の Lv2 が出る
            // Running Lv2 yields the target's Lv2
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            AdvanceTicksWithFullPower(TicksToComplete(recipe), processor);
            Assert.AreEqual(1, CountItem(inventory, InputSlotCount, InputSlotCount + OutputSlotCount, ForUnitTestLevelInheritanceIds.CappedTarget(2)));
        }

        [Test]
        public void RecipeChangeRefundsSourceAboveCapTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 2));

            // 上限なしで入れた Lv3 は、上限つきレシピへ切り替えると返却される
            // A Lv3 inserted under the uncapped recipe is refunded when switching to the capped one
            var overflow = CreateOverflow(10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, processor.SetSelectedRecipe(LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid), overflow));
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(2, CountItem(overflow, 0, 10, ForUnitTestLevelInheritanceIds.Source(3)));
        }

        [Test]
        public void CraftCapIsRestoredFromResearchOnLoadTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(serviceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            serviceProvider.GetService<IResearchDataStore>().CompleteResearch(ForUnitTestLevelInheritanceIds.CraftCapResearchGuid, playerId);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 研究の再実行で上限2が戻り、二重適用もしない
            // Replaying the research restores cap 2 without double-applying
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);
            var lookup = loadServiceProvider.GetService<ILevelInheritanceCraftCapLookup>();
            Assert.AreEqual(2, lookup.GetCraftCap(ForUnitTestLevelInheritanceIds.CappedTargetBaseGuid));
            Assert.AreEqual(1, lookup.UnlockedCaps.Count);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void RetainedAboveCapInputWaitsForResearchTest(bool loadedInput)
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(provider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid);
            if (loadedInput)
            {
                MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
                inventory.SetItem(0, Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            }
            else
            {
                MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid));
                inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
                Assert.AreEqual(MachineRecipeSelectionResult.Success, processor.SetSelectedRecipe(recipe, CreateOverflow(0)));
            }
            // 返却残余もロード残留も解放前には消費しない
            // Neither refund residue nor loaded input is consumed before unlocking
            AdvanceTicksWithFullPower(2, processor);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(2, inventory.GetItem(0).Count);
            Assert.IsTrue(provider.GetService<IResearchDataStore>().CompleteResearch(ForUnitTestLevelInheritanceIds.CraftCapResearchGuid, playerId));
            AdvanceTicksWithFullPower(TicksToComplete(recipe), processor);
            Assert.AreEqual(1, CountItem(inventory, InputSlotCount, InputSlotCount + OutputSlotCount, ForUnitTestLevelInheritanceIds.CappedTarget(2)));
        }

    }
}
