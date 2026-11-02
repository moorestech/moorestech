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
        private const int FailedDieSlot = FirstOutputSlot + 3;
        private const int FailedDieMaxStack = 1000000;
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

            // 失敗ダイのスロットを満杯にしてウェハを入れ、待機中に抽選を確定させる（失敗枠0.2なので失敗ダイ0個はまず無い）
            // Fill the failed-die slot and load a wafer so idle fixes a draw (the 0.2 failure slot makes zero failed dies practically impossible)
            inventory.SetItem(FailedDieSlot, ForUnitTestItemId.TestFailedDie, FailedDieMaxStack);
            SelectRecipeAndLoad(machine, ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, ForUnitTestItemId.TestLayeredWafer, 1);
            Assert.IsNull(ReadDicing(packet, machine).PendingDraw);
            RunTicks(filter, machine, 3);
            var processor = machine.GetComponent<CleanRoomMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Idle, processor.CurrentState);

            // モジュールを外すと次の新規抽選はTier0・低減なしへ下がり、待っている抽選は確定時のTier1の値のまま返る
            // Removing the module drops the next new draw to tier 0 with no reduction, while the waiting draw keeps its draw-time tier-1 values
            inventory.SetItem(FirstModuleSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunTicks(filter, machine, 1);
            var view = ReadDicing(packet, machine);
            AssertDicing(view.Dicing, 0.15, 0, new[] { 0.2, 0d, 0d }, 0.8);
            Assert.IsNotNull(view.PendingDraw);
            AssertDicing(view.PendingDraw, 0.03, 1, new[] { 0.7, 0.1, 0d }, 0.2);

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
                // 復元直後は部屋効果の初回push前で1tickだけHaltedを経由するので、出力待ちのIdleへ戻るまで進める
                // Before the first room-effect push a restored machine passes one Halted tick, so advance until it is back to output-blocked Idle
                RunTicks(filter, machine, 2);
                Assert.AreEqual(ProcessState.Idle, processor.CurrentState);
                AssertDicing(ReadDicing(packet, machine).PendingDraw, 0.03, 1, new[] { 0.7, 0.1, 0d }, 0.2);
            }

            // 出力を空けると確定済みの抽選で加工が始まり、待っていた抽選は応答から消える
            // Freeing the output starts work on the fixed draw, and the waiting draw leaves the response
            inventory.SetItem(FailedDieSlot, ServerContext.ItemStackFactory.CreatEmpty());
            RunTicks(filter, machine, 1);
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            Assert.IsNull(ReadDicing(packet, machine).PendingDraw);

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
            var layerExposure = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateLayerExposure(0.1, null);
            layerExposure.PendingDicing = new DicingYieldMessagePack(0.1, 1, new[] { 0.7, 0.1, 0d }, 0.2);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] LayerExposure response carries a fixed draw of another role"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, layerExposure.TryReadView(out _));

            var notOperable = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNotOperable();
            notOperable.PendingLayerExposure = new LayerExposureYieldMessagePack(0.1);
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

        private static CleanRoomYieldDicingView ReadDicing(PacketResponseCreator packet, IBlock machine)
        {
            var request = new GetCleanRoomYieldInfoProtocol.RequestCleanRoomYieldInfoMessagePack(machine.BlockInstanceId);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            var response = MessagePackSerializer.Deserialize<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack>(responseBytes[0]);
            Assert.AreEqual(BlockInfoReadResult.Success, response.TryReadView(out var view), $"kind={response.Kind}");
            Assert.IsInstanceOf<CleanRoomYieldDicingView>(view);
            return (CleanRoomYieldDicingView)view;
        }

        private static void AssertDicing(DicingYieldMessagePack dicing, double dieLossRate, int qualityTier, double[] levelRates, double failedRate)
        {
            Assert.AreEqual(dieLossRate, dicing.DieLossRate, Tolerance);
            Assert.AreEqual(qualityTier, dicing.QualityTier);
            Assert.That(dicing.LevelRates, Is.EqualTo(levelRates).Within(Tolerance));
            Assert.AreEqual(failedRate, dicing.FailedRate, Tolerance);
        }
    }
}
