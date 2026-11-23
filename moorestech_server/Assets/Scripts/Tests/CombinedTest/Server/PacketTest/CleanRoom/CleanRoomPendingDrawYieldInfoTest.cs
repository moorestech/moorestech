using System;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.CleanRoom.Machine;
using Game.Block.Blocks.Machine;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Server.PacketTest.CleanRoom
{
    /// <summary>
    ///     出力満杯で確定済み抽選が待っている間も確率は今の条件の1組だけで、加工時間は確定時の効果で決まることを固定する（裁定 2026-11-21）
    ///     Pins that while a fixed draw waits on full outputs the odds are a single set under current conditions, and that processing time is fixed by the draw-time effect (ruling 2026-11-21)
    /// </summary>
    public class CleanRoomPendingDrawYieldInfoTest
    {
        // テストの品質モジュールは tier1・効果1.0・時間のトレードオフ0.5（時間倍率1.5）
        // The test quality module is tier 1, effect 1.0, time tradeoff 0.5 (time multiplier 1.5)
        private const int TickCap = 4000;
        private const double Tolerance = 1e-5;
        private const int FailedDieSlot = FirstOutputSlot + 3;
        private const int FailedDieMaxStack = 1000000;
        private static readonly Guid TestQualityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000005");

        [TestCase(false)]
        [TestCase(true)]
        public void OddsFollowCurrentConditionsWhileADrawWaitsAndProcessingTimeStaysTest(bool saveReload)
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            IOpenableBlockInventoryComponent inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(TestQualityModuleItemGuid), 1);
            Assert.IsTrue(WaitForClassA(serviceProvider.GetService<CleanRoomDatastore>(), filter, machine, TickCap));

            // 失敗ダイのスロットを満杯にしてウェハを入れ、待機中に抽選を確定させる。確率はクラスA×tier1 の1組
            // Fill the failed-die slot and load a wafer so idle fixes a draw; the odds are the single class-A tier-1 set
            inventory.SetItem(FailedDieSlot, ForUnitTestItemId.TestFailedDie, FailedDieMaxStack);
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWafer, 1);
            AssertOdds(ReadDicing(packet, machine).Odds, new[] { 0.491749, 0.061316, 0d }, 0.446935);
            RunTicks(filter, machine, 3);
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);

            // モジュールを外すと、確定済み抽選が待っていても確率は今の条件（クラスA・モジュール無し）の1組だけになる
            // Removing the module turns the odds into the single current set (class A, no module) even with a fixed draw waiting
            inventory.SetItem(FirstModuleSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunTicks(filter, machine, 1);
            AssertOdds(ReadDicing(packet, machine).Odds, new[] { 0.17, 0d, 0d }, 0.83);

            if (saveReload)
            {
                // 部屋と機械を含むワールド全体を保存し、別DIで復元する（確定済み抽選はセーブに残る）
                // Save the whole world including the room and machine, then restore through another DI container (the fixed draw stays in the save)
                var filterPos = filter.BlockPositionInfo.OriginalPos;
                var machinePos = machine.BlockPositionInfo.OriginalPos;
                var before = Newtonsoft.Json.JsonConvert.SerializeObject(processor.GetSaveState());
                var text = SaveLoadJsonTestHelper.AssembleSaveJson(serviceProvider);
                (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
                SaveLoadJsonTestHelper.LoadFromJson(serviceProvider, text);
                filter = ServerContext.WorldBlockDatastore.GetBlock(filterPos);
                machine = ServerContext.WorldBlockDatastore.GetBlock(machinePos);
                inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
                processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
                Assert.AreEqual(before, Newtonsoft.Json.JsonConvert.SerializeObject(processor.GetSaveState()));
                Assert.IsTrue(WaitForClassA(serviceProvider.GetService<CleanRoomDatastore>(), filter, machine, TickCap));

                // 復元直後は部屋効果の初回push前で1tickだけHaltedを経由するので、出力待ちのIdleへ戻るまで進める
                // Before the first room-effect push a restored machine passes one Halted tick, so advance until it is back to output-blocked Idle
                RunTicks(filter, machine, 2);
                Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
                AssertOdds(ReadDicing(packet, machine).Odds, new[] { 0.17, 0d, 0d }, 0.83);
            }

            // 出力を空けると確定済みの抽選で加工が始まる。確率は今の条件のまま
            // Freeing the output starts work on the fixed draw; the odds stay at the current conditions
            inventory.SetItem(FailedDieSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunTicks(filter, machine, 1);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            AssertOdds(ReadDicing(packet, machine).Odds, new[] { 0.17, 0d, 0d }, 0.83);

            // 要求電力ちょうどの1tickで進捗が 1/総tick になる。総tickは外す前の品質モジュールの時間倍率1.5で決まっている
            // One tick at exactly the requested power advances progress by 1/total; the total was fixed by the removed quality module's x1.5
            EnsureFilterWiredPower(filter);
            processor.SupplyExternalPower(processor.EffectiveRequestPower);
            GameUpdater.UpdateOneTick();
            var totalTicks = Math.Round(1d / GetCommonMachineState(machine).ProcessingRate);
            Assert.AreEqual(Math.Round(GameUpdater.SecondsToTicks(1) * 1.5d), totalTicks);
        }

        private static void RunTicks(IBlock filter, IBlock machine, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                TopUpIpa(machine, 20);
                TickRoom(filter, machine);
            }
        }

        private static CleanRoomYieldDicingView ReadDicing(PacketResponseCreator packet, IBlock machine)
        {
            var request = new GetCleanRoomYieldInfoProtocol.RequestCleanRoomYieldInfoMessagePack(machine.BlockInstanceId);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            var response = MessagePackSerializer.Deserialize<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack>(responseBytes[0]);
            Assert.AreEqual(BlockInfoReadResult.Success, response.TryReadView(out var view), $"kind={response.Kind}");
            Assert.IsInstanceOf<CleanRoomYieldDicingView>(view);
            return (CleanRoomYieldDicingView)view;
        }

        private static void AssertOdds(DicingOddsMessagePack odds, double[] levelRates, double failedRate)
        {
            Assert.AreEqual(300, odds.OutputCount);
            Assert.That(odds.LevelRates, Is.EqualTo(levelRates).Within(Tolerance));
            Assert.AreEqual(failedRate, odds.FailedRate, Tolerance);
        }
    }
}
