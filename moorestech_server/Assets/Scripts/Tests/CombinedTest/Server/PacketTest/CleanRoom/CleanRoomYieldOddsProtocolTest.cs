using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Core.CleanRoom;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomMachineTestHelpers;
using static Tests.CombinedTest.Core.CleanRoom.Machine.CleanRoomProcessMachineTestHelpers;
using Response = Server.Protocol.PacketResponse.GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack;

namespace Tests.CombinedTest.Server.PacketTest.CleanRoom
{
    /// <summary>
    ///     確率の取得が最終の内訳（足して1）と改善の差分を運び、壊れた改善の一覧を読めない扱いにすることを固定する
    ///     Pins that the draw-detail query carries final odds (summing to 1) and improvement deltas, and treats a broken improvement list as unreadable
    /// </summary>
    public class CleanRoomYieldOddsProtocolTest
    {
        private const int TickCap = 4000;
        private const double Tolerance = 1e-5;
        private static readonly Guid TestQualityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000005");

        [Test]
        public void DicingCarriesFinalOddsAndModuleImprovementsTest()
        {
            var (packet, machine) = SetUpClassAMachine(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe, 1);
            var view = ReadView<CleanRoomYieldDicingView>(Invoke(packet, machine.BlockInstanceId));

            // クラスA×tier1 1個の最終の内訳: 出力300個・Lv1 0.491749・Lv2 0.061316・失敗 0.446935（足して1）
            // Final odds for class A with one tier-1 module: 300 outputs, Lv1 0.491749, Lv2 0.061316, failure 0.446935 (summing to 1)
            Assert.AreEqual(300, view.Odds.OutputCount);
            Assert.That(view.Odds.LevelRates, Is.EqualTo(new[] { 0.491749, 0.061316, 0d }).Within(Tolerance));
            Assert.AreEqual(0.446935, view.Odds.FailedRate, Tolerance);
            Assert.AreEqual(1d, view.Odds.LevelRates.Sum() + view.Odds.FailedRate, 1e-9);

            // クラスAより上は無いので部屋クラスの行は無く、tier1 を増やす行・tier2 を刺す行の順に並ぶ
            // Nothing is above class A, so there is no room-class row; the rows are one more tier 1, then one tier 2
            Assert.AreEqual(2, view.Improvements.Count);
            Assert.IsFalse(view.Improvements[0].TryGetRoomClassUp(out _));
            Assert.IsTrue(view.Improvements[0].TryGetAddQualityModule(out var firstTier));
            Assert.AreEqual(1, firstTier);
            Assert.That(view.Improvements[0].LevelRateDeltas, Is.EqualTo(new[] { 0.118365, 0.022557, 0d }).Within(Tolerance));
            Assert.AreEqual(-0.140922, view.Improvements[0].FailedRateDelta, Tolerance);
            Assert.IsTrue(view.Improvements[1].TryGetAddQualityModule(out var secondTier));
            Assert.AreEqual(2, secondTier);
            Assert.That(view.Improvements[1].LevelRateDeltas, Is.EqualTo(new[] { -0.376748, 0.450245, 0.073080 }).Within(Tolerance));
            Assert.AreEqual(-0.146577, view.Improvements[1].FailedRateDelta, Tolerance);

            // どの行の Lv別差分も、同じ応答の最終の内訳と同じ長さ（クライアントが Lv に当てる約束）
            // Every row's per-level deltas match the length of the same response's final odds (the contract the client relies on to map levels)
            foreach (var improvement in view.Improvements) Assert.AreEqual(view.Odds.LevelRates.Length, improvement.LevelRateDeltas.Length);
        }

        [Test]
        public void LayerExposureImprovementsCarryEmptyLevelDeltasTest()
        {
            var (packet, machine) = SetUpClassAMachine(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, 0);
            var view = ReadView<CleanRoomYieldLayerExposureView>(Invoke(packet, machine.BlockInstanceId));

            // クラスA×モジュール無しの失敗は 0.10。クラスAより上は無く、tier1 1個で 0.10×(1−0.8)=0.02 になる1行だけ
            // Class A without modules fails at 0.10; nothing is above class A, so the only row is one tier-1 module giving 0.10 x (1 - 0.8) = 0.02
            Assert.AreEqual(0.10, view.Odds.FailedRate, Tolerance);
            Assert.AreEqual(1, view.Improvements.Count);
            Assert.IsTrue(view.Improvements[0].TryGetAddQualityModule(out var tier));
            Assert.AreEqual(1, tier);
            Assert.AreEqual(-0.08, view.Improvements[0].FailedRateDelta, Tolerance);

            // 露光の Lv別差分は wire を通しても null でなく空の配列
            // Layer exposure's per-level deltas arrive through the wire as an empty array, not null
            Assert.IsNotNull(view.Improvements[0].LevelRateDeltas);
            Assert.IsEmpty(view.Improvements[0].LevelRateDeltas);
        }

        [Test]
        public void SaturatedLayerExposureCarriesFailureWithoutImprovementsTest()
        {
            var (packet, machine) = SetUpClassAMachine(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe, 1);
            var view = ReadView<CleanRoomYieldLayerExposureView>(Invoke(packet, machine.BlockInstanceId));

            // 失敗は 0.10×(1−0.8)=0.02。品質は頭打ちでクラスAより上も無いので、改善の行は無い（モーダルのボタンを出さない条件）
            // The failure is 0.10 x (1 - 0.8) = 0.02; quality is saturated and nothing is above class A, so no rows (the modal button stays hidden)
            Assert.AreEqual(0.02, view.Odds.FailedRate, Tolerance);
            Assert.IsEmpty(view.Improvements);
        }

        [Test]
        public void BrokenImprovementsAreUnreadableWithWarningTest()
        {
            // Lv別差分の長さが最終の内訳と違う行は、壊れた wire として警告し読めない扱いにする
            // A row whose per-level deltas differ in length from the final odds is broken wire: it warns and is unreadable
            var odds = new DicingOddsMessagePack(300, new[] { 0.5, 0.1, 0d }, 0.4);
            var shortDeltas = new List<CleanRoomYieldImprovementMessagePack> { CleanRoomYieldImprovementMessagePack.CreateAddQualityModule(1, new[] { 0.1, 0.1 }, -0.2) };
            var dicing = Response.CreateDicing(new DicingYieldMessagePack(0.1, 1, new[] { 0.6, 0.1, 0d }, 0.3), null, odds, shortDeltas);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] Dicing response has broken improvements"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, dicing.TryReadView(out _));

            // クラス名の無い RoomClassUp も、wire を通した後で読めない
            // A RoomClassUp without a class name is unreadable after the wire as well
            var noClassName = CleanRoomYieldImprovementMessagePack.CreateRoomClassUp(null, Array.Empty<double>(), -0.05);
            var layer = Response.CreateLayerExposure(0.1, null, new LayerExposureOddsMessagePack(0.1), new List<CleanRoomYieldImprovementMessagePack> { noClassName });
            var received = MessagePackSerializer.Deserialize<Response>(MessagePackSerializer.Serialize(layer));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] LayerExposure response has broken improvements"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, received.TryReadView(out _));

            // 最終の内訳が欠けた応答は入れ子の欠けとして読めない
            // A response missing its final odds is unreadable as a missing nested object
            var noOdds = Response.CreateDicing(new DicingYieldMessagePack(0.1, 1, new[] { 0.6, 0.1, 0d }, 0.3), null, null, new List<CleanRoomYieldImprovementMessagePack>());
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] Dicing response has no"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, noOdds.TryReadView(out _));
        }

        [Test]
        public void RoomClassImprovementAndResponseHeaderSurviveWireTest()
        {
            var improvements = new List<CleanRoomYieldImprovementMessagePack>
            {
                CleanRoomYieldImprovementMessagePack.CreateRoomClassUp("B", Array.Empty<double>(), -0.05),
            };
            var response = Response.CreateLayerExposure(0.1, null, new LayerExposureOddsMessagePack(0.1), improvements);
            response.SequenceId = 42;
            var received = MessagePackSerializer.Deserialize<Response>(MessagePackSerializer.Serialize(response));

            // 応答のヘッダとクラス昇格の行を同じシリアライズ経路で確認する
            // Exercise the response header and room-class row through the same serialization path
            Assert.AreEqual(GetCleanRoomYieldInfoProtocol.ProtocolTag, received.Tag);
            Assert.AreEqual(42, received.SequenceId);
            var view = ReadView<CleanRoomYieldLayerExposureView>(received);
            Assert.AreEqual(1, view.Improvements.Count);
            Assert.IsTrue(view.Improvements[0].TryGetRoomClassUp(out var targetClassName));
            Assert.AreEqual("B", targetClassName);
            Assert.AreEqual(-0.05, view.Improvements[0].FailedRateDelta, Tolerance);
        }

        private static (PacketResponseCreator packet, IBlock machine) SetUpClassAMachine(Guid recipeGuid, int tier1ModuleCount)
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            if (0 < tier1ModuleCount) machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(TestQualityModuleItemGuid), tier1ModuleCount);
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid));
            Assert.IsTrue(WaitForClassA(serviceProvider.GetService<CleanRoomDatastore>(), filter, machine, TickCap));

            // クラスAに上がったtickの効果の押し込みを、もう1tick回して確実に済ませる
            // Run one more tick so the effect push for the tick that reached class A has surely happened
            TickRoom(filter, machine);
            return (packet, machine);
        }

        private static Response Invoke(PacketResponseCreator packet, BlockInstanceId id)
        {
            var request = new GetCleanRoomYieldInfoProtocol.RequestCleanRoomYieldInfoMessagePack(id);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            return MessagePackSerializer.Deserialize<Response>(responseBytes[0]);
        }

        private static TView ReadView<TView>(Response response) where TView : CleanRoomYieldView
        {
            Assert.AreEqual(BlockInfoReadResult.Success, response.TryReadView(out var view), $"kind={response.Kind}");
            Assert.IsInstanceOf<TView>(view);
            return (TView)view;
        }
    }
}
