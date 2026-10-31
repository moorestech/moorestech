using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory.Input;
using Game.Block.Interface.Component;
using Game.Block.Interface.MachineUpgrade;
using Core.Master;
using Tests.Util;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.InputLevelStall.MachineInputLevelStallTestUtil;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;
using static Tests.Module.TestMod.ForUnitTestLevelInheritanceIds;

namespace Tests.CombinedTest.Core.Machine.Upgrade.InputLevelStall
{
    /// <summary>
    ///     ベルトの無い最小構成で、止まった理由が出たまま残らないことと、出してはいけない場面で出ないことを検証する
    ///     In minimal setups without a belt, verifies the stall never stays on and never appears where it must not
    /// </summary>
    public class MachineInputLevelStallMinimalTest
    {
        [Test]
        public void SingleBlockInsertWithoutRetryClearsWithinTwoTicksTest()
        {
            var (block, inventory) = SetUpMachine(InheritRecipeGuid);
            inventory.SetItem(0, Create(Source(1), 1));

            // ブロック搬入の口で1回だけ断られると、その時点では止まった理由が立つ
            // A single refusal at the block-inflow inlet reports the stall at that moment
            Assert.AreEqual(1, inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty).Count);
            AssertStalled(block, MachineInputLevelRefusalReason.MixedLevel, Source(2));

            // 渡し直す搬入元が無いので、2tick以内に消える
            // With no source re-offering, it clears within two ticks
            RunTicks(2);
            AssertNotStalled(block);
        }

        [Test]
        public void ManualInsertNeverStallsTest()
        {
            var (block, inventory) = SetUpMachine(CappedInheritRecipeGuid);

            // プレイヤーの挿入（文脈なしの InsertItem。InventoryItemInsertService と同じ口）は断られても理由を立てない
            // A player insert (context-less InsertItem, the same entry as InventoryItemInsertService) never reports a stall even when refused
            Assert.AreEqual(1, inventory.InsertItem(Create(Source(2), 1)).Count);
            AssertNotStalled(block);
        }

        [Test]
        public void SatisfiedRecipeDoesNotStallTest()
        {
            var (block, inventory) = SetUpMachine(InheritRecipeGuid);
            inventory.SetItem(0, Create(Source(1), 2));
            inventory.SetItem(1, CreateTest1(1));

            // 材料がそろっていれば機械は動けるので、別レベルが断られても止まった扱いにしない
            // With the recipe satisfied the machine can run, so a refused other level does not count as a stall
            Assert.AreEqual(1, inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty).Count);
            AssertNotStalled(block);
        }

        [Test]
        public void SourceSatisfiedButOtherInputMissingDoesNotStallTest()
        {
            var (block, inventory) = SetUpMachine(InheritRecipeGuid);
            inventory.SetItem(0, Create(Source(1), 2));

            // 継承元は要求数(2)そろい Test1 だけが欠けている。レベル違いを仕分けても動かないので理由にしない
            // The source meets its requirement (2) and only Test1 is missing; sorting levels would not start it, so no stall
            Assert.AreEqual(1, inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty).Count);
            AssertNotStalled(block);
        }

        [Test]
        public void RefusalWhileProcessingStallsOnlyAfterJobEndsTest()
        {
            var (block, inventory) = SetUpMachine(InheritRecipeGuid);
            var processor = block.GetComponent<VanillaMachineProcessorComponent>();
            inventory.SetItem(0, Create(Source(1), 3));
            inventory.SetItem(1, CreateTest1(2));

            // 1tick で加工が始まり、継承元は残り1（要求2に足りない）になる
            // One tick starts the job and leaves one source, short of the required two
            AdvanceTicksWithFullPower(1, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(1, CountItem(inventory, 0, InputSlotCount, Source(1)));

            // 加工中に Lv2 が断られても、次の加工の見込みにすぎないので出さない
            // A Lv2 refused while processing is only a forecast for the next job, so no stall
            Assert.AreEqual(1, inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty).Count);
            AssertNotStalled(block);

            // 加工が終わって待機に戻り、始められない間に断られると出る
            // Once the job ends and the machine idles unable to start, a refusal reports the stall
            AdvanceTicksWithFullPower(TicksToComplete(LevelInheritanceTestHelper.Recipe(InheritRecipeGuid)), processor);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(1, inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty).Count);
            AssertStalled(block, MachineInputLevelRefusalReason.MixedLevel, Source(2));
        }

        [Test]
        public void NonLevelRefusalDoesNotStallTest()
        {
            var (block, inventory) = SetUpMachine(InheritRecipeGuid);

            // 継承元でない品（束縛外）が断られても止まった理由にしない（従来の差し戻し）
            // A refused non-source item (outside the binding) is not a stall reason (the usual bounce)
            Assert.AreEqual(1, inventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 1), InsertItemContext.Empty).Count);
            AssertNotStalled(block);

            // レシピ未選択の機械は何も受けず、止まった理由も立てない
            // A machine without a selected recipe accepts nothing and reports no stall
            var (unselectedBlock, unselectedInventory, _) = PlaceMachine(new Vector3Int(5, 0, 0));
            Assert.AreEqual(1, unselectedInventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty).Count);
            AssertNotStalled(unselectedBlock);
        }
        [Test]
        public void ResearchAndRecipeChangesClearRefusalWithinSameTickTest()
        {
            var (block, inventory) = SetUpMachine(CappedInheritRecipeGuid);
            inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty);
            AssertStalled(block, MachineInputLevelRefusalReason.AboveCraftCap, Source(2));
            var action = MasterHolder.ResearchMaster.GetResearch(CraftCapResearchGuid).ClearedActions.items[0];
            ServerContext.GetService<ILevelInheritanceCraftCapUnlocker>().ApplyUnlockLevelInheritanceCraftLevelAction(action);
            AssertNotStalled(block);
            inventory.SetItem(0, Create(Source(1), 1));
            inventory.InsertItem(Create(Source(2), 1), InsertItemContext.Empty);
            AssertStalled(block, MachineInputLevelRefusalReason.MixedLevel, Source(2));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(SubstituteRecipeGuid));
            AssertNotStalled(block);
        }

    }
}
