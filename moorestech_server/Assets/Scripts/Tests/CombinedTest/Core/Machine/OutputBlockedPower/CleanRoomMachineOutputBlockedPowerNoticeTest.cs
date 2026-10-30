using Core.Item;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.CombinedTest.Core.CleanRoom;
using Tests.Module.TestMod;
using Tests.Util;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Core.Machine
{
    // 清浄室機械も出力詰まりの間、毎tickラッチした電力の分子分母が状態イベントで届く（ADR 0010）
    // Clean-room machines also deliver each tick's latched power pair through state events while output-blocked (ADR 0010)
    public class CleanRoomMachineOutputBlockedPowerNoticeTest
    {
        private const int TickCap = 4000;
        private const int SpanTicks = 3;

        [Test]
        public void 清浄室機械の出力詰まり中は待機要求の充足と断電と復電が最新の通知に届く()
        {
            var datastore = CleanRoomHatchTest.CreateServer();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, TickCap));
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            DriveIntoOutputBlocked(filter, machine, processor);

            // 詰まり中の電力網への要求は待機要求（マスタ値からの独立計算）
            // The grid request while blocked is the idle request, computed independently from master values
            var param = (CleanRoomMachineBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.CleanRoomProcessMachineId).BlockParam;
            var idleRequest = processor.RequestPower * param.IdlePowerRate;
            Assert.AreEqual(idleRequest, processor.EffectiveRequestPower, 0.01f, "出力詰まり中の電力網への要求が待機要求ではない");

            // 待機要求を満たす→断電→復電の各区間で、最後の通知がその区間のラッチ値を持つ
            // In each span (idle request met, power cut, power back) the last notice carries that span's latched values
            var notices = MachinePowerNoticeRecorder.Subscribe(processor);
            var before = notices.Count;
            TickRoomWithMachineSupply(filter, processor, idleRequest);
            notices.AssertLastNotice(before, idleRequest, idleRequest, "待機要求を満たした詰まり");

            before = notices.Count;
            TickRoomWithMachineSupply(filter, processor, 0f);
            notices.AssertLastNotice(before, 0f, idleRequest, "詰まり中の断電");

            before = notices.Count;
            TickRoomWithMachineSupply(filter, processor, idleRequest);
            notices.AssertLastNotice(before, idleRequest, idleRequest, "詰まり中の復電");
            Assert.AreEqual(ProcessState.OutputBlocked, processor.CurrentState);
        }

        // 原料1枚とIPAで加工へ入れてから全出口をG1上限で埋め、完了tickを跨いで出力詰まりへ入れる
        // Start a job from one raw wafer and IPA, fill every outlet to the G1 cap, then cross the completion tick into blockage
        private static void DriveIntoOutputBlocked(IBlock filter, IBlock machine, CleanRoomMachineProcessorComponent processor)
        {
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 1);
            TopUpIpa(machine, 20);
            TickRoom(filter, machine);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            var inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ForUnitTestItemId.TestGradedWaferG1);
            for (var slot = FirstOutputSlot; slot < FirstOutputSlot + OutputSlotCount; slot++) inventory.SetItem(slot, ForUnitTestItemId.TestGradedWaferG1, maxStack);
            for (var tick = 0; tick < TickCap && processor.CurrentState != ProcessState.OutputBlocked; tick++) TickRoom(filter, machine);
            Assert.AreEqual(ProcessState.OutputBlocked, processor.CurrentState);
        }

        private static void TickRoomWithMachineSupply(IBlock filter, CleanRoomMachineProcessorComponent processor, float power)
        {
            for (var tick = 0; tick < SpanTicks; tick++)
            {
                EnsureFilterWiredPower(filter);
                processor.SupplyExternalPower(power);
                GameUpdater.UpdateOneTick();
            }
        }
    }
}
