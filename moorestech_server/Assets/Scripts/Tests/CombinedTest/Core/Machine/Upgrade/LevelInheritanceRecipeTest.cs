using Core.Item;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Core.Machine.Upgrade
{
    /// <summary>
    ///     継承レシピが入力のレベルを別ファミリーの出力へ移し、混ぜず、返却・待機キャッシュでもレベルを保つことを検証する
    ///     Verifies an inheritance recipe carries the input level into another family's output, never mixes, and keeps the level in refunds and the idle cache
    /// </summary>
    public class LevelInheritanceRecipeTest
    {
        [Test]
        public void InheritsSourceLevelAcrossFamiliesTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);

            // Lv3 の継承元と固定素材を入れて1回分回す
            // Insert a Lv3 source plus the fixed input and run one craft
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 2));
            inventory.InsertItem(CreateTest1(1));
            AdvanceTicksWithFullPower(TicksToComplete(recipe), processor);

            // 別ファミリーの Lv3 が出て、Lv1 は出ない
            // The other family's Lv3 comes out and no Lv1 does
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual(1, CountItem(inventory, InputSlotCount, InputSlotCount + OutputSlotCount, ForUnitTestLevelInheritanceIds.Target(3)));
            Assert.AreEqual(0, CountItem(inventory, InputSlotCount, InputSlotCount + OutputSlotCount, ForUnitTestLevelInheritanceIds.Target(1)));
        }

        [Test]
        public void SourceLevelsNeverMixInSlotTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, _) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid));

            // Lv2 が入っている継承元スロットへ Lv3 は重ならず、全量が戻る
            // A Lv3 never stacks onto the Lv2 already in the source slot; the whole stack bounces back
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(2), 1));
            var remainder = inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 2));
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(3), remainder.Id);
            Assert.AreEqual(2, remainder.Count);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(2), inventory.GetItem(0).Id);
            Assert.AreEqual(1, inventory.GetItem(0).Count);
        }

        [Test]
        public void RecipeChangeRefundsInheritedLevelTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid));
            inventory.InsertItem(Create(ForUnitTestLevelInheritanceIds.Source(3), 2));
            inventory.InsertItem(CreateTest1(1));

            // 加工開始（入力消費）まで進める
            // Advance until processing starts and the inputs are consumed
            AdvanceTicksWithFullPower(2, processor);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.AreEqual(0, inventory.GetItem(0).Count);

            // 選択解除で返却され、継承元は Lv3 のまま戻る（返却は入力スロット→溢れ先の順なので両方を数える）
            // Clearing the selection refunds the job and the source comes back as Lv3 (refunds go to input slots first, then overflow, so count both)
            var overflow = CreateOverflow(10);
            Assert.AreEqual(MachineRecipeSelectionResult.Success, processor.ClearSelectedRecipe(overflow));
            Assert.AreEqual(2, CountItem(inventory, 0, InputSlotCount, ForUnitTestLevelInheritanceIds.Source(3)) + CountItem(overflow, 0, 10, ForUnitTestLevelInheritanceIds.Source(3)));
            Assert.AreEqual(0, CountItem(inventory, 0, InputSlotCount, ForUnitTestLevelInheritanceIds.Source(1)) + CountItem(overflow, 0, 10, ForUnitTestLevelInheritanceIds.Source(1)));
        }

        [Test]
        public void IdleCacheFollowsSourceLevelChangeTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = LevelInheritanceTestHelper.Recipe(ForUnitTestLevelInheritanceIds.InheritRecipeGuid);
            var (block, inventory, processor) = PlaceMachine(new Vector3Int(1, 1, 1));
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);

            // 出力スロットを Lv1 で満杯にし、Lv2 の実現出力を待機中に確定させる
            // Fill the output slot with Lv1 so the Lv2 realized output is fixed while waiting
            var targetMax = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestLevelInheritanceIds.Target(1));
            inventory.SetItem(InputSlotCount, Create(ForUnitTestLevelInheritanceIds.Target(1), targetMax));
            inventory.SetItem(0, Create(ForUnitTestLevelInheritanceIds.Source(2), 2));
            inventory.SetItem(1, CreateTest1(1));
            AdvanceTicksWithFullPower(3, processor);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);

            // 待機中に継承元を Lv3 へ差し替えてから出力を空ける
            // Swap the source to Lv3 while waiting, then free the output
            inventory.SetItem(0, Create(ForUnitTestLevelInheritanceIds.Source(3), 2));
            AdvanceTicksWithFullPower(1, processor);
            inventory.SetItem(InputSlotCount, ServerContext.ItemStackFactory.CreatEmpty());
            AdvanceTicksWithFullPower(TicksToComplete(recipe), processor);

            // 古い Lv2 の確定結果を使い回さず、Lv3 が出る
            // The stale Lv2 result is not reused; Lv3 comes out
            Assert.AreEqual(1, CountItem(inventory, InputSlotCount, InputSlotCount + OutputSlotCount, ForUnitTestLevelInheritanceIds.Target(3)));
            Assert.AreEqual(0, CountItem(inventory, InputSlotCount, InputSlotCount + OutputSlotCount, ForUnitTestLevelInheritanceIds.Target(2)));
        }
    }
}
