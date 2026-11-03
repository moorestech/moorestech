using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Newtonsoft.Json;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
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
        private static readonly Guid TestQualityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000005");
        private static readonly Guid TestQualityFractionalModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000006");

        [Test]
        public void NoQualityModuleYieldsOnlyLevelOneOrFailedTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();

            // 品質モジュール無しはTier0の表（Lv1と失敗ダイだけ）なので、Lv2以上は出ない
            // No quality module uses the tier-0 table (only Lv1 and failed dies), so Lv2 and above never appear
            var collected = DiceWafers(filter, machine, 2);

            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestDieLv2));
            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestDieLv3));
            Assert.Greater(CountOf(collected, ForUnitTestItemId.TestDieLv1), 0);
            Assert.AreEqual(DiesPerWafer * 2, CountOf(collected, ForUnitTestItemId.TestDieLv1) + CountOf(collected, ForUnitTestItemId.TestFailedDie));
        }

        [Test]
        public void TierOneModuleYieldsLevelOneMostlyAndLevelTwoRarelyTest()
        {
            var collected = DiceInClassAWithModule(TestQualityModuleItemGuid);

            // Tier1・クラスA・1個はシフト0で表[.7,.1,0]のまま。Lv2はレア（約50個）で、Lv3は品質ゲートで出ない
            // Tier 1, class A, one module: zero shift keeps the table [.7,.1,0]; Lv2 is the rare (~50) and the quality gate blocks Lv3
            var levelOne = CountOf(collected, ForUnitTestItemId.TestDieLv1);
            var levelTwo = CountOf(collected, ForUnitTestItemId.TestDieLv2);
            Assert.Greater(levelOne, levelTwo);
            Assert.Greater(levelTwo, 0);
            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestDieLv3));
        }

        [Test]
        public void TierTwoModuleFailureSlotContainsLevelOneTest()
        {
            var collected = DiceInClassAWithModule(TestQualityFractionalModuleItemGuid);

            // Tier2の表[.1,.7,.1]: Lv2が最多で、失敗枠のLv1も出る
            // Tier 2 table [.1,.7,.1]: Lv2 dominates while Lv1 still appears as part of the failure slot
            var levelOne = CountOf(collected, ForUnitTestItemId.TestDieLv1);
            var levelTwo = CountOf(collected, ForUnitTestItemId.TestDieLv2);
            Assert.Greater(levelOne, 0);
            Assert.Greater(levelTwo, levelOne);
            Assert.Greater(levelTwo, CountOf(collected, ForUnitTestItemId.TestDieLv3));
        }

        [Test]
        public void FullOutputSlotHoldsJobWithoutLossTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();

            // 失敗ダイのスロットを満杯にすると、抽選済みの失敗ダイ（Tier0は失敗枠0.8なので0個はまず無い）が入らず加工が始まらない
            // With the failed-die slot full, the drawn failed dies (tier 0 has a 0.8 failure slot, so zero is practically impossible) cannot fit and the job never starts
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWafer, 1);
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

            // 生産性効果1.0は追加1セットを必ず出す。ウェハ1枚から同じ抽選の2セット（計600個）が出て、各スロットに収まる
            // A productivity effect of 1.0 always adds one set: one wafer yields two copies of one draw (600 dies) that fit their slots
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWafer, 1);
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

        [Test]
        public void SaveLoadPreservesCycleCounterTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            DiceWafers(filter, machine, 1);

            // ダイシングは役割レシピなので抽選のたびにカウンタが進む
            // Dicing is a role recipe, so every draw advances the counter
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            Assert.GreaterOrEqual(((CleanRoomMachineProcessorSaveJsonObject)processor.GetSaveState()).CycleCount, 1u);

            // blockInstanceIdとcycleCountが保存され、リロード後の状態が一致する
            // blockInstanceId and cycleCount persist, so the reloaded state matches
            var positionInfo = new BlockPositionInfo(new Vector3Int(30, 0, 30), BlockDirection.North, Vector3Int.one);
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomProcessMachineId).BlockGuid;
            var reloaded = ServerContext.BlockFactory.Load(blockGuid, machine.BlockInstanceId, SaveLoadJsonTestHelper.ThroughJson(machine.GetSaveState()), positionInfo);
            Assert.AreEqual(JsonConvert.SerializeObject(processor.GetSaveState()), JsonConvert.SerializeObject(reloaded.GetComponent<CleanRoomMachineProcessorComponent>().GetSaveState()));
        }

        #region TestHelper

        private static Dictionary<ItemId, int> DiceWafers(IBlock filter, IBlock machine, int waferCount)
        {
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWafer, waferCount);
            return RunAndCollect(filter, machine, DiesPerWafer * waferCount, TickCap);
        }

        // クラスAで品質モジュール1個を挿してウェハ2枚を切る（クラスAの倍率1でシフト0になる）
        // Dice two wafers in class A with one quality module (class A's multiplier of 1 gives zero shift)
        private static Dictionary<ItemId, int> DiceInClassAWithModule(Guid moduleItemGuid)
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(moduleItemGuid), 1);
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, TickCap));
            return DiceWafers(filter, machine, 2);
        }

        #endregion
    }
}
