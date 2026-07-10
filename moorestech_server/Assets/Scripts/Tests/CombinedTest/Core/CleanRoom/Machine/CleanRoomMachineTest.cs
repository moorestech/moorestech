using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util.MachineRecipe;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom
{
    public class CleanRoomMachineTest
    {
        [Test]
        public void MachineOutsideRoomStaysHaltedAndDrawsNoPowerTest()
        {
            CleanRoomHatchTest.CreateServer();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(20, 0, 20));

            // 部屋外の機械に材料と満電を与え、清浄度なしでは進まないことを固定する
            // Feed a powered machine outside any room and lock in that no clean class means no work
            LoadMachineInput(machine, 1);
            TickWithPower(machine, 100f, 25);

            // 停止中は進捗だけでなく電力要求と材料消費も止まる
            // While halted, both energy demand and ingredient consumption stay frozen
            Assert.AreEqual(ProcessState.Halted, machine.GetComponent<CleanRoomMachineProcessorComponent>().CurrentState);
            Assert.AreEqual(0, machine.GetComponent<IElectricConsumer>().RequestEnergy.AsPrimitive());
            // GetItemの多重定義曖昧さを避けるためIOpenableInventoryとして扱う
            // View as IOpenableInventory to avoid the GetItem overload ambiguity
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            Assert.AreEqual(1, inventory.GetItem(0).Count);
        }

        [Test]
        public void MachineInsideAirClassRoomProcessesRecipeTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();

            // 同じ室内に機械を置き、清浄化後に通常レシピが進むことを確認する
            // Place the machine in the same room and verify the recipe runs after purification
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(2, 1, 1));
            LoadMachineInput(machine, 5);

            // 検出と清浄度の更新周期に依存しないよう、成果物が出るまで上限付きで待つ
            // Poll with a cap so the test does not depend on exact detection or purity tick timing
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            for (var i = 0; i < 800 && !IsAnyChipLevel(inventory.GetItem(2).Id); i++)
            {
                TickRoom(filter, machine);
            }

            // 室内がOutではない清浄度へ到達したことを、レシピ完了とは別に検証する
            // Verify separately from recipe completion that the room reached a non-Out clean class
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));
            Assert.Less(room.ThresholdIndex, MasterHolder.CleanRoomMaster.OutThresholdIndex);

            // クラスA室では4レベル一様抽選なので、具体レベルは固定しない
            // Class-A rooms draw uniformly across four levels, so the exact level is intentionally not fixed
            Assert.IsTrue(IsAnyChipLevel(inventory.GetItem(2).Id));
            Assert.GreaterOrEqual(inventory.GetItem(2).Count, 1);
            Assert.Less(inventory.GetItem(0).Count, 5);
        }

        [Test]
        public void InterruptedProcessingFreezesThenResumesAfterRoomReformsTest()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(2, 1, 1));

            // 処理途中で止めるため、まず清浄室内で機械を実際にProcessingへ入れる
            // First move the machine into Processing inside a clean room so interruption freezes real work
            LoadMachineInput(machine, 5);
            var filterConsumer = filter.GetComponent<IElectricConsumer>();
            var machineConsumer = machine.GetComponent<IElectricConsumer>();
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();

            // 清浄度の収束まで待ち、機械が加工状態へ入ったことを明示的に保証する
            // Wait for purity convergence and explicitly prove the machine entered processing
            for (var i = 0; i < 200 && processor.CurrentState != ProcessState.Processing; i++) TickRoom();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            // 完了前の進捗を作ってから壁を壊し、途中停止の対象を明確にする
            // Accumulate partial progress before breaking the wall so the freeze target is unambiguous
            for (var i = 0; i < 5; i++) TickRoom();
            Assert.AreEqual(0, inventory.GetItem(2).Count);

            // 壁欠損で部屋が消えた後、機械がHaltedへ遷移するまで待つ
            // After the wall breach removes the room, wait for the machine to transition to Halted
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 1, 1), BlockRemoveReason.ManualRemove);
            for (var i = 0; i < 50 && processor.CurrentState != ProcessState.Halted; i++) TickRoom();
            Assert.AreEqual(ProcessState.Halted, processor.CurrentState);
            Assert.AreEqual(0, machineConsumer.RequestEnergy.AsPrimitive());

            // 長い停止中も出力が生えず、処理が勝手に完了しないことを確認する
            // During a long outage, output must not appear and processing must not complete silently
            for (var i = 0; i < 100; i++) TickRoom();
            Assert.AreEqual(ProcessState.Halted, processor.CurrentState);
            Assert.AreEqual(0, inventory.GetItem(2).Count);

            // 壁を戻しても換気0ならOutに留まり、給電中の機械だけが停止し続ける
            // Restoring the wall with no ventilation keeps the room Out, so the powered machine must stay halted
            CleanRoomDetectionTest.AddBlock(ForUnitTestModBlockId.CleanRoomWallId, new Vector3Int(0, 1, 1));
            for (var i = 0; i < 20; i++) TickMachineOnly();
            Assert.AreEqual(ProcessState.Halted, processor.CurrentState);
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var reformedRoom));
            Assert.AreEqual(MasterHolder.CleanRoomMaster.OutThresholdIndex, reformedRoom.ThresholdIndex);

            // 再収束後にProcessingへ戻り、凍結していたジョブが完了することを確認する
            // After reconvergence, the frozen job should resume Processing and then complete
            for (var i = 0; i < 200 && processor.CurrentState != ProcessState.Processing; i++) TickRoom();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsTrue(datastore.TryGetCleanRoomAt(new Vector3Int(1, 1, 1), out var room));
            Assert.Less(room.ThresholdIndex, MasterHolder.CleanRoomMaster.OutThresholdIndex);
            for (var i = 0; i < 200 && !IsAnyChipLevel(inventory.GetItem(2).Id); i++) TickRoom();
            Assert.IsTrue(IsAnyChipLevel(inventory.GetItem(2).Id));
            Assert.GreaterOrEqual(inventory.GetItem(2).Count, 1);

            #region Internal

            void TickRoom()
            {
                // 中断中も両方へ満電を供給し、停止判定が電力不足と混ざらないようにする
                // Keep both blocks fully powered during interruption so halted state is not confused with low power
                filterConsumer.SupplyEnergy(new ElectricPower(100f));
                machineConsumer.SupplyEnergy(new ElectricPower(100f));
                GameUpdater.UpdateOneTick();
            }

            void TickMachineOnly()
            {
                // フィルターを止めたまま機械だけ給電し、Out判定のゲートだけを検証する
                // Power only the machine while the filter is stopped to isolate the Out-class gate
                machineConsumer.SupplyEnergy(new ElectricPower(100f));
                GameUpdater.UpdateOneTick();
            }

            #endregion
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

        private static void LoadMachineInput(IBlock machine, int count)
        {
            MachineRecipeSelectionTestUtil.SelectRecipe(machine, ForUnitTestMachineRecipeId.CleanRoomMachineRecipe);
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(0, ForUnitTestItemId.TestChipRawWafer, count);
        }

        private static bool IsAnyChipLevel(ItemId itemId)
        {
            return itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv1) ||
                   itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv2) ||
                   itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv3) ||
                   itemId.Equals(ForUnitTestItemId.TestSemiconductorChipLv4);
        }

        private static void TickRoom(IBlock filter, IBlock machine)
        {
            // フィルターと機械を同じtickで給電し、室内加工の通常経路を進める
            // Power the filter and machine in the same tick to exercise normal in-room processing
            filter.GetComponent<IElectricConsumer>().SupplyEnergy(new ElectricPower(100f));
            machine.GetComponent<IElectricConsumer>().SupplyEnergy(new ElectricPower(100f));
            GameUpdater.UpdateOneTick();
        }

        private static void TickWithPower(IBlock block, float power, int ticks)
        {
            // 既存の電力系テストと同じくConsumerへ毎tick直接供給する
            // Supply the consumer directly every tick, matching existing powered block tests
            var consumer = block.GetComponent<IElectricConsumer>();
            for (var i = 0; i < ticks; i++)
            {
                consumer.SupplyEnergy(new ElectricPower(power));
                GameUpdater.UpdateOneTick();
            }
        }

        #endregion
    }
}
