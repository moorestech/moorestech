using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using Tests.Util.EnergySystem;
using Core.Inventory;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using MessagePack;
using NUnit.Framework;
using System.Linq;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.CleanRoom.Machine
{
    public class CleanRoomMachineTest
    {
        // 期待値をproduction式から独立させるためのマスタ実値（forUnitTest mod の TestCleanRoomMachine）
        // Master values kept independent from the production formula (TestCleanRoomMachine in the forUnitTest mod)
        private const float CleanRoomMachineRequiredPower = 100f;
        private const float CleanRoomMachineIdlePowerRate = 0.2f;

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
            // 清浄機は電線経由の出力可変発電機で給電し、停電をSetPowerで再現できるようにする
            // Power the filter through a settable wired generator so an outage can be simulated via SetPower
            var filterGenerator = ElectricWireTestUtil.WirePower(filter.BlockPositionInfo.OriginalPos, new Vector3Int(30, 0, 30), 100f);
            var machineConsumer = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            IOpenableInventory inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();

            // 清浄度の収束まで待ち、機械が加工状態へ入ったことを明示的に保証する
            // Wait for purity convergence and explicitly prove the machine entered processing
            for (var i = 0; i < 200 && processor.CurrentState != ProcessState.Processing; i++) TickRoom();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            // 遷移直後のstateは分子CurrentPowerと同じ前tick(Idle)基準でラッチされる（期待値はマスタ値からの独立計算）
            // Right after the transition the state latches on the previous tick's Idle basis, matching the numerator CurrentPower (expected value computed independently from master values)
            Assert.AreEqual(CleanRoomMachineRequiredPower * CleanRoomMachineIdlePowerRate, GetCommonMachineState(machine).RequestPower, 0.01f);

            // 次tickで加工基準の実効要求電力（モジュール0枠なので倍率1）へ揃う
            // The next tick moves it to the processing basis (multiplier 1 because the machine has no module slots)
            TickRoom();
            Assert.AreEqual(CleanRoomMachineRequiredPower, GetCommonMachineState(machine).RequestPower, 0.01f);

            // 完了前の進捗を作ってから壁を壊し、途中停止の対象を明確にする
            // Accumulate partial progress before breaking the wall so the freeze target is unambiguous
            for (var i = 0; i < 5; i++) TickRoom();
            Assert.AreEqual(0, inventory.GetItem(2).Count);

            // 壁欠損で部屋が消えた後、機械がHaltedへ遷移するまで待つ
            // After the wall breach removes the room, wait for the machine to transition to Halted
            ServerContext.WorldBlockDatastore.RemoveBlock(new Vector3Int(0, 1, 1), BlockRemoveReason.ManualRemove);
            for (var i = 0; i < 50 && processor.CurrentState != ProcessState.Halted; i++) TickRoom();
            Assert.AreEqual(ProcessState.Halted, processor.CurrentState);
            Assert.AreEqual(0, machineConsumer.EffectiveRequestPower);
            // Halted中はstateの要求電力も0で張り付くこと（基礎値へ戻す退行を検出する）
            // The state's request power must also latch to 0 while halted, catching a regression to the base value
            Assert.AreEqual(0f, GetCommonMachineState(machine).RequestPower, 0.01f);

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
                filterGenerator.SetPower(new ElectricPower(100f));
                machineConsumer.SupplyExternalPower(100f);
                GameUpdater.UpdateOneTick();
            }

            void TickMachineOnly()
            {
                // フィルター側の発電を止めたまま機械だけ給電し、Out判定のゲートだけを検証する
                // Stop the filter-side generation and power only the machine to isolate the Out-class gate
                filterGenerator.SetPower(new ElectricPower(0f));
                machineConsumer.SupplyExternalPower(100f);
                GameUpdater.UpdateOneTick();
            }

            #endregion
        }

    }
}
