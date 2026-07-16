using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomChipOutputTest
    {
        [Test]
        public void CompletedCycleOutputsOneOfTheChipLevelsTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(2, 1, 1));

            // EUV失敗を許容するため複数枚を投入し、成功したチップ出力だけを待つ
            // Insert several wafers to tolerate EUV failures and wait only for successful chip output
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(System.Guid.Parse("19b0d248-0ce5-4e5f-b59c-5897177b6268"));
            MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestChipRawWafer, 5);
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();

            for (var i = 0; i < 800 && CountChipOutputs(inventory) == 0; i++)
            {
                TickRoom(filter, machine);
            }

            // クラスA室ではLv1固定ではなく4レベル一様抽選なので、具体レベルは固定しない
            // Class-A rooms draw uniformly across four levels, so this intentionally does not assert Lv1
            var outputCount = CountChipOutputs(inventory);
            Assert.Greater(outputCount, 0);
            Assert.LessOrEqual(outputCount, 5);
        }

        [Test]
        public void SaveLoadPreservesCycleCounterTest()
        {
            CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(2, 1, 1));

            // 出力の有無ではなく状態遷移で1サイクル完了を観測し、EUV失敗に依存しない
            // Observe one full cycle via state transitions rather than output so EUV failure is harmless
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(System.Guid.Parse("19b0d248-0ce5-4e5f-b59c-5897177b6268"));
            MachineRecipeSelectTestUtil.SelectRecipe(machine, recipe);
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestChipRawWafer, 1);
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            var enteredProcessing = false;
            for (var i = 0; i < 400; i++)
            {
                TickRoom(filter, machine);
                if (processor.CurrentState == ProcessState.Processing) enteredProcessing = true;
                if (enteredProcessing && processor.CurrentState == ProcessState.Idle) break;
            }

            Assert.IsTrue(enteredProcessing);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);

            // blockInstanceIdとcycleCountが保存されるため、リロード後の次回抽選も決定論的に一致する
            // Preserving blockInstanceId and cycleCount proves the next draw after reload is deterministic
            var positionInfo = new BlockPositionInfo(new Vector3Int(30, 0, 30), BlockDirection.North, Vector3Int.one);
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomMachineId).BlockGuid;
            var states = machine.GetSaveState();
            var reloaded = ServerContext.BlockFactory.Load(blockGuid, machine.BlockInstanceId, states, positionInfo);

            Assert.AreEqual(processor.GetSaveState(), reloaded.GetComponent<CleanRoomMachineProcessorComponent>().GetSaveState());
        }

        #region TestHelper

        // 内寸3x3x3の密閉室に満電用フィルターを1台置く
        // Build a sealed 3x3x3-interior room with one filter prepared for full-power operation
        private static IBlock BuildSmallCleanRoomWithFilter()
        {
            CleanRoomDetectionTest.BuildBox(new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            var filter = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, new Vector3Int(1, 1, 1));
            filter.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestCleanRoomFilter, 5);
            return filter;
        }

        private static void TickRoom(IBlock filter, IBlock machine)
        {
            // フィルターと機械を同じtickで給電し、室内加工の通常経路を進める
            // Power the filter and machine in the same tick to exercise normal in-room processing
            ElectricConsumerTestUtil.ApplySuppliedPower(filter.GetComponent<IElectricConsumer>(), 100f);
            ElectricConsumerTestUtil.ApplySuppliedPower(machine.GetComponent<IElectricConsumer>(), 100f);
            GameUpdater.UpdateOneTick();
        }

        private static int CountChipOutputs(IOpenableInventory inventory)
        {
            var chipIds = GetChipItemIds();
            var totalCount = 0;

            for (var slot = 2; slot <= 3; slot++)
            {
                var item = inventory.GetItem(slot);
                for (var i = 0; i < chipIds.Length; i++)
                {
                    if (item.Id.Equals(chipIds[i])) totalCount += item.Count;
                }
            }

            return totalCount;
        }

        private static ItemId[] GetChipItemIds()
        {
            return new[]
            {
                ForUnitTestItemId.TestSemiconductorChipLv1,
                ForUnitTestItemId.TestSemiconductorChipLv2,
                ForUnitTestItemId.TestSemiconductorChipLv3,
                ForUnitTestItemId.TestSemiconductorChipLv4,
            };
        }

        #endregion
    }
}
