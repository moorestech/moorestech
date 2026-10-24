using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    public class CleanRoomDicingTest
    {
        private const int DiesPerWafer = 300;
        private const int TickCap = 4000;
        private const int FailedDieSlot = FirstOutputSlot + 3;
        private const int FailedDieMaxStack = 1000000;
        private static readonly Guid TestProductivityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");

        [Test]
        public void GradeThreeWaferSplitsAcrossThreeLevelsTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();

            // G3ウェハ2枚は600個に切られ、Lv1〜Lv3と失敗ダイに分かれる（Lv3が0個の確率は約0.87^255）
            // Two G3 wafers become 600 dies split into Lv1-Lv3 and failed dies (no Lv3 has odds ~0.87^255)
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWaferG3, 2);
            var collected = RunAndCollect(filter, machine, DiesPerWafer * 2, TickCap);

            var levelOne = CountOf(collected, ForUnitTestItemId.TestDieLv1);
            var levelTwo = CountOf(collected, ForUnitTestItemId.TestDieLv2);
            var levelThree = CountOf(collected, ForUnitTestItemId.TestDieLv3);
            Assert.AreEqual(DiesPerWafer * 2, levelOne + levelTwo + levelThree + CountOf(collected, ForUnitTestItemId.TestFailedDie));
            Assert.Greater(levelOne, 0);
            Assert.Greater(levelThree, 0);
        }

        [Test]
        public void GradeOneWaferYieldsOnlyLevelOneTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();

            // G1の表は[1]なので、良品は全部Lv1（品質ゲート）
            // G1's table is [1], so every good die is Lv1 (the quality gate)
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWaferG1, 2);
            var collected = RunAndCollect(filter, machine, DiesPerWafer * 2, TickCap);

            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestDieLv2));
            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestDieLv3));
            Assert.AreEqual(DiesPerWafer * 2, CountOf(collected, ForUnitTestItemId.TestDieLv1) + CountOf(collected, ForUnitTestItemId.TestFailedDie));
        }

        [Test]
        public void FullOutputSlotHoldsJobWithoutLossTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();

            // 失敗ダイのスロットを満杯にすると、抽選済みの失敗ダイ（損失0個の確率は0.9^300未満）が入らず加工が始まらない
            // With the failed-die slot full, the drawn failed dies (zero losses has odds below 0.9^300) cannot fit and the job never starts
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWaferG1, 1);
            inventory.SetItem(FailedDieSlot, ForUnitTestItemId.TestFailedDie, FailedDieMaxStack);
            for (var i = 0; i < 600; i++)
            {
                TopUpIpa(machine, 20);
                TickRoom(filter, machine);
            }
            Assert.AreEqual(1, inventory.GetItem(InputSlot).Count);
            Assert.AreNotEqual(ProcessState.Processing, machine.GetComponent<CleanRoomMachineProcessorComponent>().CurrentState);

            // スロットを空けると始まり、300個が欠けずに出る
            // Freeing the slot lets the job start, and all 300 dies come out
            inventory.SetItem(FailedDieSlot, ServerContext.ItemStackFactory.CreatEmpty());
            var collected = RunAndCollect(filter, machine, DiesPerWafer, TickCap);
            Assert.AreEqual(0, inventory.GetItem(InputSlot).Count);
            Assert.AreEqual(DiesPerWafer, CountOf(collected, ForUnitTestItemId.TestDieLv1) + CountOf(collected, ForUnitTestItemId.TestFailedDie));
        }

        [Test]
        public void ProductivityExtraSetSharesTheDrawAndFitsTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(TestProductivityModuleItemGuid), 1);

            // 生産性効果1.0は追加1セットを必ず出す。G3ウェハ1枚から同じ抽選の2セット（計600個）が出て、各スロットに収まる
            // A productivity effect of 1.0 always adds one set: one G3 wafer yields two copies of one draw (600 dies) that fit their slots
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWaferG3, 1);
            var collected = RunAndCollect(filter, machine, DiesPerWafer * 2, TickCap);

            var counts = new[]
            {
                CountOf(collected, ForUnitTestItemId.TestDieLv1),
                CountOf(collected, ForUnitTestItemId.TestDieLv2),
                CountOf(collected, ForUnitTestItemId.TestDieLv3),
                CountOf(collected, ForUnitTestItemId.TestFailedDie),
            };
            Assert.AreEqual(0, inventory.GetItem(InputSlot).Count);
            Assert.AreEqual(DiesPerWafer * 2, counts.Sum());

            // 追加セットは同じ抽選結果を並べるので、どの出力も2の倍数になる
            // The extra set repeats the same draw, so every output is a multiple of two
            foreach (var count in counts) Assert.AreEqual(0, count % 2, $"counts={string.Join(",", counts)}");
        }
    }
}
