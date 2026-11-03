using System;
using System.Text.RegularExpressions;
using Core.Master;
using Core.Update;
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

namespace Tests.CombinedTest.Server.PacketTest.CleanRoom
{
    public class GetCleanRoomYieldInfoProtocolTest
    {
        // テストマスタのクラスA: 品質倍率1.0・層失敗率0.10・ダイ損失率0.15。低減の上限は0.8
        // Test master class A: quality x1.0, layer fail 0.10, die loss 0.15; the reduction cap is 0.8
        private const int TickCap = 4000;
        private const double Tolerance = 1e-6;
        private static readonly Guid TestQualityModuleItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000005");

        [Test]
        public void DepositionReportsNoRoleTest()
        {
            var (packet, machine) = SetUpOperableMachine(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe);
            Assert.IsTrue(machine.GetComponent<IMachineRecipeSelectorComponent>().TryGetSelectedRecipe(out var selected));
            Assert.AreEqual(ForUnitTestMachineRecipeId.CleanRoomDepositionRecipe, selected.MachineRecipeGuid);

            // 成膜は役割表に無い普通の工程なので、稼働できる部屋でも抽選の中身を持たない
            // Deposition is a plain process outside the role tables, so it carries no draw details even in an operable room
            ReadView<CleanRoomYieldNoRoleView>(Invoke(packet, machine.BlockInstanceId));
        }

        [Test]
        public void LayerExposureShowsLayerFailRateTest()
        {
            var (packet, machine) = SetUpOperableMachine(ForUnitTestMachineRecipeId.CleanRoomLayerExposureRecipe);

            // 層失敗率は 0.10×(1−min(1.0, 0.8))=0.02
            // The layer fail rate is 0.10 x (1 - min(1.0, 0.8)) = 0.02
            var view = ReadView<CleanRoomYieldLayerExposureView>(Invoke(packet, machine.BlockInstanceId));
            Assert.AreEqual(0.02, view.LayerExposure.LayerFailRate, Tolerance);
        }

        [Test]
        public void DicingShowsQualityTierAndLevelRatesTest()
        {
            var (packet, machine) = SetUpOperableMachine(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe);
            var view = ReadView<CleanRoomYieldDicingView>(Invoke(packet, machine.BlockInstanceId));

            // ダイ損失率は 0.15×(1−min(1.0, 0.8))=0.03
            // The die loss rate is 0.15 x (1 - min(1.0, 0.8)) = 0.03
            Assert.AreEqual(0.03, view.Dicing.DieLossRate, Tolerance);

            // tier1・クラスA・1個はシフト0なので、Tier1の表 [.7,.1,0]・失敗0.2のまま返る
            // One tier-1 module in class A gives zero shift, so the tier-1 table [.7,.1,0] with 0.2 failed comes back unchanged
            Assert.AreEqual(1, view.Dicing.QualityTier);
            Assert.That(view.Dicing.LevelRates, Is.EqualTo(new[] { 0.7, 0.1, 0d }).Within(Tolerance));
            Assert.AreEqual(0.2, view.Dicing.FailedRate, Tolerance);
            Assert.IsNull(view.PendingDraw);
        }

        [Test]
        public void MachineOutsideRoomReturnsNotOperableTest()
        {
            var (packet, _) = CreatePacket();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomProcessMachineId, new Vector3Int(10, 0, 10));
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.CleanRoomDicingRecipe));
            GameUpdater.UpdateOneTick();

            // 役割のあるレシピでも、部屋の外の機械は稼働できず、クラス値を持たないので数字を返さない
            // A machine outside any room cannot operate and has no class values, so no numbers are returned
            ReadView<CleanRoomYieldNotOperableView>(Invoke(packet, machine.BlockInstanceId));
        }

        [Test]
        public void NoSelectedRecipeReturnsNoRoleTest()
        {
            var (packet, _) = CreatePacket();
            var machine = CleanRoomHatchTest.PlaceBlock(ForUnitTestModBlockId.CleanRoomProcessMachineId, new Vector3Int(10, 0, 10));

            // レシピ未選択は工程の役割が無いので NoRole（未選択は TryGetSelectedRecipe の false で表す）
            // No selected recipe means no process role, so NoRole (unselected is TryGetSelectedRecipe returning false)
            Assert.IsFalse(machine.GetComponent<IMachineRecipeSelectorComponent>().TryGetSelectedRecipe(out _));
            ReadView<CleanRoomYieldNoRoleView>(Invoke(packet, machine.BlockInstanceId));
        }

        [Test]
        public void UnknownBlockReturnsBlockNotFoundTest()
        {
            var (packet, _) = CreatePacket();

            // 存在しないブロックIDは BlockNotFound を返し、ビューを持たない
            // A missing block ID returns BlockNotFound, which has no view
            var response = Invoke(packet, new BlockInstanceId(999999));
            Assert.AreEqual(CleanRoomYieldResponseKind.BlockNotFound, response.Kind);
            Assert.AreEqual(BlockInfoReadResult.BlockNotFound, response.TryReadView(out _));
        }

        [Test]
        public void NonInteriorMachineReturnsUnsupportedTargetWithWarningTest()
        {
            var (packet, _) = CreatePacket();
            var chest = BeltWorldTestUtil.Place(ForUnitTestModBlockId.ChestId, new Vector3Int(30, 0, 30), BlockDirection.North);

            // 室内機械でないブロックは、正常な NoRole と区別して非対応の対象として返す
            // A block that is not an interior machine comes back as an unsupported target, apart from a genuine NoRole
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] block .* has no CleanRoomYieldPreviewComponent; returning UnsupportedTarget"));
            var response = Invoke(packet, chest.BlockInstanceId);
            Assert.AreEqual(CleanRoomYieldResponseKind.UnsupportedTarget, response.Kind);
            Assert.AreEqual(BlockInfoReadResult.UnsupportedTarget, response.TryReadView(out var view));
            Assert.IsNull(view);
        }

        [Test]
        public void MalformedResponsesAreUnreadableWithWarningTest()
        {
            // 判別子に対応する入れ子が欠けた応答（壊れた・版違いのwire）は、警告を出して読めない扱いにする
            // A response missing the nested object its discriminator names (broken or mismatched wire) warns and is unreadable
            var layerExposure = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateLayerExposure(0.1, null);
            layerExposure.LayerExposure = null;
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] LayerExposure response has no"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, layerExposure.TryReadView(out _));

            // wire を通しても同じ（入れ子が null のまま届く）
            // The same holds through the wire (the nested object arrives as null)
            var dicing = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateDicing(new DicingYieldMessagePack(0.1, 1, new[] { 0.7, 0.1, 0d }, 0.2), null);
            dicing.Dicing = null;
            var received = MessagePackSerializer.Deserialize<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack>(MessagePackSerializer.Serialize(dicing));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] Dicing response has no"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, received.TryReadView(out _));

            // ダイシングのレベル別割合が欠けた応答も、次の抽選・確定済み抽選のどちらでも読めない
            // A dicing response missing its level rates is unreadable too, for both the next draw and a fixed draw
            var noLevelRates = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateDicing(new DicingYieldMessagePack(0.1, 1, null, 0.2), null);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] Dicing response has no"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, noLevelRates.TryReadView(out _));
            var pendingNoLevelRates = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateDicing(new DicingYieldMessagePack(0.1, 1, new[] { 0.7, 0.1, 0d }, 0.2), new DicingYieldMessagePack(0.1, 1, null, 0.2));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[GetCleanRoomYieldInfo\] Dicing response has no"));
            Assert.AreEqual(BlockInfoReadResult.Unreadable, pendingNoLevelRates.TryReadView(out _));
        }

        private static (PacketResponseCreator packet, IBlock machine) SetUpOperableMachine(Guid recipeGuid)
        {
            var (packet, datastore) = CreatePacket();
            var filter = BuildSmallCleanRoomWithFilter();
            var machine = PlaceProcessMachine();
            machine.GetComponent<IOpenableBlockInventoryComponent>().SetItem(FirstModuleSlot, MasterHolder.ItemMaster.GetItemId(TestQualityModuleItemGuid), 1);
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(recipeGuid));
            Assert.IsTrue(WaitForClassA(datastore, filter, machine, TickCap));

            // クラスAに上がったtickの効果の押し込みを、もう1tick回して確実に済ませる
            // Run one more tick so the effect push for the tick that reached class A has surely happened
            TickRoom(filter, machine);
            return (packet, machine);
        }

        private static (PacketResponseCreator packet, CleanRoomDatastore datastore) CreatePacket()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return (packet, serviceProvider.GetService<CleanRoomDatastore>());
        }

        private static GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack Invoke(PacketResponseCreator packet, BlockInstanceId id)
        {
            var request = new GetCleanRoomYieldInfoProtocol.RequestCleanRoomYieldInfoMessagePack(id);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            return MessagePackSerializer.Deserialize<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack>(responseBytes[0]);
        }

        // 応答を役割ごとの型で読み、期待した型であることを確かめる
        // Read the response as its per-role type and check it is the expected one
        private static TView ReadView<TView>(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack response) where TView : CleanRoomYieldView
        {
            Assert.AreEqual(BlockInfoReadResult.Success, response.TryReadView(out var view), $"kind={response.Kind}");
            Assert.IsInstanceOf<TView>(view);
            return (TView)view;
        }
    }
}
