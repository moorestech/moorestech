using Core.Item;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using NUnit.Framework;
using Tests.CombinedTest.Core.CleanRoom;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;
using static Tests.CombinedTest.Core.Machine.MachineOutputSpaceShortTestHelper;

namespace Tests.CombinedTest.Core.Machine
{
    // 清浄室機械も通常機械と同じ出力空き不足の理由を状態詳細で出す（Task31 r3-B F01）
    // Clean-room machines publish the same output-space-short reason as normal machines (Task31 r3-B F01)
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

            // 全出力スロットをG1ウェハーの上限で埋めてから、ウェハー1枚とIPAを入れる
            // Fill every output slot to the G1 wafer cap, then load one wafer and IPA
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestGradedWaferG1);
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 1);
            for (var slot = FirstOutputSlot; slot < FirstOutputSlot + OutputSlotCount; slot++) inventory.SetItem(slot, ForUnitTestItemId.TestGradedWaferG1, maxStack);
            TopUpIpa(machine, 20);
            TickRoom(filter, machine);
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
            Assert.AreEqual("idle", ReadPublishedState(processor));
            Assert.IsTrue(ReadOutputSpaceShort(processor));

            // 出口を空けると加工が始まり理由が消える
            // Freeing the output starts the job and clears the reason
            for (var slot = FirstOutputSlot; slot < FirstOutputSlot + OutputSlotCount; slot++) inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
            TickRoom(filter, machine);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsFalse(ReadOutputSpaceShort(processor));
        }
    }
}
