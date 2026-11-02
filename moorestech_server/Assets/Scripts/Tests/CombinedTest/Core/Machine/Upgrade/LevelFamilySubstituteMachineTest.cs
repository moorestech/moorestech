using System;
using Core.Item;
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using UnityEngine.TestTools;
using Core.Master;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Block.Interface.MachineUpgrade;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     通常レシピの基準要求に上位の変種を充て、他ファミリーは弾き、自動搬入だけ低い方へ揃え、継承元スロットには効かないことを確かめる
    ///     Verifies a plain recipe's base requirement takes higher variants, rejects other families, aligns only automated inflow to the lower level, and leaves the inheritance source slot alone
    /// </summary>
    public class LevelFamilySubstituteMachineTest
    {
        private static readonly Guid Test2ItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [Test]
        public void HigherLevelFulfilsBaseRequirementTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);

            // Lv1 を要求する素材へ Lv2 を入れて1回分回す
            // Put Lv2 into the input that requires Lv1 and run one craft
            Assert.AreEqual(0, inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2)).Count);
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(TicksToComplete(recipe), processor);

            // Lv2 が消費され、製品が出る
            // The Lv2 is consumed and the product comes out
            Assert.AreEqual(0, inventory.GetItem(0).Count);
            Assert.AreEqual(1, CountItem(inventory, InputSlotCount, InputSlotCount + OutputSlotCount, MasterHolder.ItemMaster.GetItemId(Test2ItemGuid)));
        }

        [Test]
        public void OtherFamilyIsRejectedTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, _) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));

            // 別ファミリーの Lv2 は手動でも自動搬入でも入らない
            // Another family's Lv2 enters neither by hand nor by automated inflow
            Assert.AreEqual(2, inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Target(2), 2)).Count);
            Assert.AreEqual(2, inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Target(2), 2), InsertItemContext.Empty).Count);
            Assert.AreEqual(0, inventory.GetItem(0).Count);
        }

        [Test]
        public void ManualInsertDoesNotMixLevelsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, _) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));

            // 手動の挿入では Lv1 の上へ Lv2 は重ならず、そのまま戻る
            // A manual insert never stacks Lv2 onto Lv1; it comes straight back
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(1), 1));
            var remainder = inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 1));
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(2), remainder.Id);
            Assert.AreEqual(1, remainder.Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(1), inventory.GetItem(0).Id);
            Assert.AreEqual(1, inventory.GetItem(0).Count);
        }

        [Test]
        public void AutomatedInflowAlignsToLowerLevelTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);

            // スロットの Lv2 へ自動搬入の Lv1 が来ると、両方 Lv1 に揃って積まれる
            // When automated inflow brings Lv1 onto the slot's Lv2, both align to Lv1 and stack
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 1));
            Assert.AreEqual(0, inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(1), 1), InsertItemContext.Empty).Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(1), inventory.GetItem(0).Id);
            Assert.AreEqual(2, inventory.GetItem(0).Count);

            // 揃えた後もそのまま加工が始まる（詰まらない）
            // Processing still starts after the alignment (no jam)
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(TicksToComplete(recipe), processor);
            Assert.AreEqual(0, inventory.GetItem(0).Count);

            // 逆向き（スロットが Lv1、来るのが Lv3）も Lv1 に揃う
            // The reverse (slot Lv1, incoming Lv3) also aligns to Lv1
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(1), 1));
            Assert.AreEqual(0, inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 1), InsertItemContext.Empty).Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(1), inventory.GetItem(0).Id);
            Assert.AreEqual(2, inventory.GetItem(0).Count);
        }

        [Test]
        public void InheritanceSourceSlotNeverAlignsTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, _) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid));

            // 継承元スロットは自動搬入でもレベルを揃えず、別レベルは戻る（継承レベルを書き換えない）
            // The inheritance source slot never aligns even for automated inflow; another level bounces (the inherited level is never rewritten)
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 1));
            var remainder = inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 1), InsertItemContext.Empty);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(3), remainder.Id);
            Assert.AreEqual(1, remainder.Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(2), inventory.GetItem(0).Id);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void RecipeChangeRefundsConsumedVariantTest(bool saveReload, bool legacy)
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(2, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            if (saveReload)
            {
                // 実際のJSON往復でも上位変種の消費記録を保つ
                // Preserve consumed higher-variant identities through real JSON serialization
                var states = SaveLoadJsonTestHelper.ThroughJson(block.GetSaveState());
                if (legacy)
                {
                    var saved = (JObject)states[VanillaMachineSaveComponent.SaveKeyStatic];
                    var job = (JObject)saved["processor"];
                    job.Property("pendingRealization").Remove();
                    job.Property("consumedInputItemGuids").Remove();
                    job.Property("totalTicks").Remove();
                    var oldWorld = new JObject { ["worldVersion"] = 7, ["world"] = new JArray(new JObject { ["state"] = new JObject { [VanillaMachineSaveComponent.SaveKeyStatic] = saved } }) };
                    Assert.IsTrue(new SaveMigrationStepV7ToV8().Migrate(oldWorld).IsConverted);
                    LogAssert.Expect(LogType.Warning, new Regex("確定結果の節が無い"));
                    LogAssert.Expect(LogType.Warning, new Regex("確定時間が無い"));
                }
                block = ServerContext.BlockFactory.Load(block.BlockGuid, block.BlockInstanceId, states,
                    new BlockPositionInfo(new Vector3Int(20, 0, 20), BlockDirection.North, Vector3Int.one));
                inventory = block.GetComponent<VanillaMachineBlockInventoryComponent>();
                processor = block.GetComponent<VanillaMachineProcessorComponent>();
            }

            // 加工中の返却は消費した実品（投入した変種）で戻る
            // A mid-process refund returns the consumed variant that was inserted (refunds go to input slots first, then overflow, so count both)
            if (legacy)
                foreach (var input in LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.SubstituteRecipeGuid).InputItems)
                    if (input.IsRemain != true) LogAssert.Expect(LogType.Warning, new Regex("消費した変種が不明で基準品で返却"));
            var overflow = CreateOverflow(10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, processor.ClearSelectedRecipe(overflow));
            Assert.AreEqual(legacy ? 0 : 2, CountItem(inventory, 0, InputSlotCount, ForUnitTestLevelInheritanceIds.Source(2)) + CountItem(overflow, 0, 10, ForUnitTestLevelInheritanceIds.Source(2)));
            Assert.AreEqual(legacy ? 2 : 0, CountItem(inventory, 0, InputSlotCount, ForUnitTestLevelInheritanceIds.Source(1)) + CountItem(overflow, 0, 10, ForUnitTestLevelInheritanceIds.Source(1)));
        }

        [Test]
        public void SubstituteLevelMatchesInheritanceSourceLevelTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.TryGetShape(LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid), out var shape));

            // 「基準の変種の何番目か」は代用（Core.Item）と継承元（Game.Block.Interface）に1本ずつあるので、同じ答えを返すことをここで固定する
            // "Which variant of the base" lives once for substitutes (Core.Item) and once for the inheritance source (Game.Block.Interface), so pin that both answer the same
            var sourceBase = ForUnitTestLevelInheritanceIds.Source(1);
            var candidates = new[] { ForUnitTestLevelInheritanceIds.Source(1), ForUnitTestLevelInheritanceIds.Source(2), ForUnitTestLevelInheritanceIds.Source(3), ForUnitTestLevelInheritanceIds.Target(2), ForUnitTestItemId.ItemId1 };
            foreach (var itemId in candidates)
            {
                Assert.AreEqual(shape.ResolveSourceLevel(itemId), LevelFamilySubstituteUtil.ResolveAcceptedLevel(sourceBase, itemId), $"itemId={itemId}");
            }
        }
    }
}
