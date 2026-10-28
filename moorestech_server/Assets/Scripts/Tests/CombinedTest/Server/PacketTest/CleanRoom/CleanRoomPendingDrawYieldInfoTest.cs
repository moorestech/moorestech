using System;
using System.Text.RegularExpressions;
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
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;

namespace Tests.CombinedTest.Server.PacketTest.CleanRoom
{
    /// <summary>
    ///     出力満杯で待っている確定済み抽選について、確定時の率が次の新規抽選の現在値と分けて返り、加工時間も確定時の効果で決まることを固定する（D02）
    ///     Pins that a fixed draw waiting on full outputs returns its draw-time rates apart from the next draw's current values, and that its processing time is fixed by the draw-time effect (D02)
    /// </summary>
    public class CleanRoomPendingDrawYieldInfoTest
    {
        // テストの品質モジュールは tier1・効果1.0・時間のトレードオフ0.5（時間倍率1.5）
        // The test quality module is tier 1, effect 1.0, time tradeoff 0.5 (time multiplier 1.5)
        private const int TickCap = 4000;
        private const double Tolerance = 1e-6;
        private static readonly Guid TestQualityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000005");

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingQualityModuleWhileOutputFullKeepsDrawTimeRatesAndProcessingTimeTest(bool saveReload)
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            IOpenableBlockInventoryComponent inventory = machine.GetComponent<IOpenableBlockInventoryComponent>();
            inventory.SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(TestQualityModuleItemGuid), 1);
            Assert.IsTrue(WaitForClassA(serviceProvider.GetService<CleanRoomDatastore>(), filter, machine, TickCap));

            // 上限G2の抽選では出ないG3で出力スロットを塞ぎ、材料を入れて待機中に抽選を確定させる
            // Block the output slot with G3, which a G2-capped draw never yields, then load inputs so idle fixes a draw
            inventory.SetItem(FirstOutputSlot, ForUnitTestItemId.TestGradedWaferG3, 1);
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, ForUnitTestItemId.TestChipRawWafer, 1);
            Assert.IsNull(ReadWaferGrade(packet, machine).PendingDraw);
            RunTicks(filter, machine, 3);
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);

            // モジュールを外すと次の新規抽選は上限G1・昇格0へ下がり、待っている抽選は確定時のG2・0.9のまま返る
            // Removing the module drops the next new draw to cap G1 and chance 0, while the waiting draw keeps its draw-time G2 and 0.9
            inventory.SetItem(FirstModuleSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunTicks(filter, machine, 1);
            var view = ReadWaferGrade(packet, machine);
            Assert.AreEqual(1, view.WaferGrade.MaxGrade);
            Assert.AreEqual(0d, view.WaferGrade.PromotionChance, Tolerance);
            Assert.IsNotNull(view.PendingDraw);
            Assert.AreEqual(2, view.PendingDraw.MaxGrade);
            Assert.AreEqual(0.9, view.PendingDraw.PromotionChance, Tolerance);

            if (saveReload)
            {
                // 部屋と機械を含むワールド全体を保存し、別DIで復元する
                // Save the whole world including the room and machine, then restore through another DI container
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
                // 復元直後の機械は部屋効果の初回push前なので、1tick進めて効果を受け取らせる
                // A just-restored machine has not received its first room-effect push, so advance one tick to deliver it
                RunTicks(filter, machine, 1);
                var restored = ReadWaferGrade(packet, machine);
                Assert.AreEqual(2, restored.PendingDraw.MaxGrade);
                Assert.AreEqual(0.9, restored.PendingDraw.PromotionChance, Tolerance);
            }

            // 出力を空けると確定済みの抽選で加工が始まり、待っていた抽選は応答から消える
            // Freeing the output starts work on the fixed draw, and the waiting draw leaves the response
            inventory.SetItem(FirstOutputSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunTicks(filter, machine, 1);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsNull(ReadWaferGrade(packet, machine).PendingDraw);

            // 要求電力ちょうどの1tickで進捗が 1/総tick になる。総tickは外す前の品質モジュールの時間倍率1.5で決まっている
            // One tick at exactly the requested power advances progress by 1/total; the total was fixed by the removed quality module's x1.5
            EnsureFilterWiredPower(filter);
            processor.SupplyExternalPower(processor.EffectiveRequestPower);
            GameUpdater.UpdateOneTick();
            var totalTicks = Math.Round(1d / GetCommonMachineState(machine).ProcessingRate);
            Assert.AreEqual(Math.Round(GameUpdater.SecondsToTicks(1) * 1.5d), totalTicks);
        }

        [Test]
        public void PendingDrawOfAnotherRoleIsUnreadableWithWarningTest()
        {
            // 判別子と違う役割の確定済み抽選や、率の無い種別に付いた確定済み抽選は、壊れた wire として警告し読めない扱いにする
            // A fixed draw of another role, or one on a kind without rates, is broken wire: it warns and is unreadable
            var waferGrade = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateWaferGrade(2, 0.5, null);
            waferGrade.PendingDicing = new DicingYieldMessagePack(0.1);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] WaferGrade response carries a fixed draw of another role"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, waferGrade.TryReadView(out _));

            var notOperable = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNotOperable();
            notOperable.PendingWaferGrade = new WaferGradeYieldMessagePack(2, 0.5);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] NotOperable response carries a fixed draw of another role"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, notOperable.TryReadView(out _));
        }

        private static void RunTicks(IBlock filter, IBlock machine, int ticks)
        {
            for (var tick = 0; tick < ticks; tick++)
            {
                TopUpIpa(machine, 20);
                TickRoom(filter, machine);
            }
        }

        private static CleanRoomYieldWaferGradeView ReadWaferGrade(PacketResponseCreator packet, IBlock machine)
        {
            var request = new GetCleanRoomYieldInfoProtocol.RequestCleanRoomYieldInfoMessagePack(machine.BlockInstanceId);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            var response = MessagePackSerializer.Deserialize<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack>(responseBytes[0]);
            Assert.AreEqual(BlockInfoReadResult.Success, response.TryReadView(out var view), $"kind={response.Kind}");
            Assert.IsInstanceOf<CleanRoomYieldWaferGradeView>(view);
            return (CleanRoomYieldWaferGradeView)view;
        }
    }
}
