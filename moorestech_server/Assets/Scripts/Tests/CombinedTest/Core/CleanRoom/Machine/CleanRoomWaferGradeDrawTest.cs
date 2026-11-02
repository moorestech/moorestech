using System;
using Core.Master;
using Game.Block.Blocks.CleanRoom.Machine;
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
    public class CleanRoomWaferGradeDrawTest
    {
        // クラスAで品質tier1・効果1.0なら昇格確率は0.9。20枚でG2が1枚も出ない確率は0.1^20
        // Class A with a tier-1 module of effect 1.0 gives a 0.9 promotion chance; no G2 in 20 wafers has odds 0.1^20
        private const int WaferCount = 20;
        private const int TickCap = 4000;
        private static readonly Guid TestQualityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000005");

        [Test]
        public void NoQualityModuleYieldsOnlyGradeOneTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, TickCap));

            // 品質モジュール無しは上限G1なので、クラスAでも全部G1になる
            // Without a quality module the cap is G1, so even class A yields only G1
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, WaferCount);
            var collected = RunAndCollect(filter, machine, WaferCount, TickCap);

            Assert.AreEqual(WaferCount, CountOf(collected, ForUnitTestItemId.TestGradedWaferG1));
            Assert.AreEqual(1, collected.Count);
        }

        [Test]
        public void TierOneQualityModuleCapsAtGradeTwoTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(TestQualityModuleItemGuid), 1);
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, TickCap));

            // tier1の品質モジュールでは上限G2。G3は出ず、G2は出る
            // A tier-1 quality module caps at G2: G3 never appears while G2 does
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, WaferCount);
            var collected = RunAndCollect(filter, machine, WaferCount, TickCap);

            Assert.AreEqual(0, CountOf(collected, ForUnitTestItemId.TestGradedWaferG3));
            Assert.AreEqual(WaferCount, CountOf(collected, ForUnitTestItemId.TestGradedWaferG1) + CountOf(collected, ForUnitTestItemId.TestGradedWaferG2));
            Assert.Greater(CountOf(collected, ForUnitTestItemId.TestGradedWaferG2), 0);
        }

        [Test]
        public void SaveLoadPreservesCycleCounterTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 2);
            RunAndCollect(filter, machine, 1, TickCap);

            // 成膜は役割レシピなので抽選のたびにカウンタが進む
            // Deposition is a role recipe, so every draw advances the counter
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            Assert.GreaterOrEqual(((CleanRoomMachineProcessorSaveJsonObject)processor.GetSaveState()).CycleCount, 1u);

            // blockInstanceIdとcycleCountが保存され、リロード後の状態が一致する
            // blockInstanceId and cycleCount persist, so the reloaded state matches
            var positionInfo = new BlockPositionInfo(new Vector3Int(30, 0, 30), BlockDirection.North, Vector3Int.one);
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomProcessMachineId).BlockGuid;
            var reloaded = ServerContext.BlockFactory.Load(blockGuid, machine.BlockInstanceId, SaveLoadJsonTestHelper.ThroughJson(machine.GetSaveState()), positionInfo);
            Assert.AreEqual(JsonConvert.SerializeObject(processor.GetSaveState()), JsonConvert.SerializeObject(reloaded.GetComponent<CleanRoomMachineProcessorComponent>().GetSaveState()));
        }
    }
}
