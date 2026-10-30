using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Update;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.CombinedTest.Core.CleanRoom;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;
using static Tests.CombinedTest.Core.Machine.MachineOutputSpaceShortTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // - 通常機械と共通の待機理由
    // - The same wait reason as normal machines
    public class CleanRoomMachineOutputSpaceShortReasonTest
    {
        private const int TickCap = 4000;

        [Test]
        public void 清浄室機械の出口が満杯の待機は理由を出し出口回収で消える()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, TickCap));
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            var inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();

            // 全出口G1上限、原料1枚とIPA投入
            // Fill all outputs to the G1 cap, then load one raw wafer and IPA
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestGradedWaferG1);
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 1);
            for (var slot = FirstOutputSlot; slot < FirstOutputSlot + OutputSlotCount; slot++) inventory.SetItem(slot, ForUnitTestItemId.TestGradedWaferG1, maxStack);
            TopUpIpa(machine, 20);
            var notices = TickRoomRecordingMachineNotices(filter, machine);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual("idle", ReadPublishedState(processor));
            Assert.IsTrue(ReadOutputSpaceShort(processor));
            Assert.IsTrue(notices.Last(), "機械の更新が理由を通知しない");

            // 出口を空けると加工が始まり理由が消える
            // Freeing the output starts the job and clears the reason
            for (var slot = FirstOutputSlot; slot < FirstOutputSlot + OutputSlotCount; slot++) inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
            TickRoom(filter, machine);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor));
        }

        [Test]
        public void 清浄室機械の保存から復元した直後の初期スナップショットが次tickを待たずに理由を持つ()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, TickCap));
            var inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestGradedWaferG1);
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 1);
            for (var slot = FirstOutputSlot; slot < FirstOutputSlot + OutputSlotCount; slot++) inventory.SetItem(slot, ForUnitTestItemId.TestGradedWaferG1, maxStack);
            TopUpIpa(machine, 20);
            Assert.IsTrue(TickRoomRecordingMachineNotices(filter, machine).Last(), "機械の更新が理由を通知しない");

            // JSON往復で復元した機械は、tickを1度も回さずに理由を持つ（保持中の確定結果・入出力・選択レシピから判定）
            // The machine restored through a JSON round trip carries the reason before any tick (judged from the held result, inventories and selection)
            var restored = ServerContext.BlockFactory.Load(machine.BlockGuid, machine.BlockInstanceId, SaveLoadJsonTestHelper.ThroughJson(machine.GetSaveState()),
                new BlockPositionInfo(new Vector3Int(30, 0, 30), BlockDirection.North, Vector3Int.one));
            var restoredProcessor = restored.GetComponent<CleanRoomMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Idle, restoredProcessor.CurrentState);
            Assert.AreEqual("idle", ReadPublishedState(restoredProcessor));
            Assert.IsTrue(ReadOutputSpaceShort(restoredProcessor));
        }

        // 明示給電の通知より後から購読する。tick内の電気網の給電通知は機械更新より前なので、最後の通知が機械更新の結果になる
        // Subscribe after the explicit supply notice; in-tick network supply notices precede the machine update, so the last notice is the update's
        private static List<bool> TickRoomRecordingMachineNotices(IBlock filter, IBlock machine)
        {
            EnsureFilterWiredPower(filter);
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            processor.SupplyExternalPower(100f);
            var notices = RecordReasonNotices(processor);
            GameUpdater.UpdateOneTick();
            return notices;
        }
    }
}
