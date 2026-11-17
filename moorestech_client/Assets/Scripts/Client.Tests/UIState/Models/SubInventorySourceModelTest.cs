using Game.Block.Interface.State;
using System.Collections.Generic;
using Game.Fluid;
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Client.Tests.Common;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.Train.Inventory;
using Core.Master;
using Core.Item.Interface;
using Game.Context;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.UIState.Models
{
    public class SubInventorySourceModelTest
    {
        // モデル化だけを見るテストなので、到達判定で引かれない空の登録簿を渡す
        // These tests only cover model building, so an empty registry the reach check never consults is passed
        private readonly TrainCarViewRegistryFake _trainCarViewRegistry = new();

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 列車ソースはコンテナ無し応答をエラー種別へ写す()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var response = new InventoryResponse(identifier, new List<IItemStack>(), new List<FluidMessagePack>(), InventoryRequestResult.ContainerNotFound);
            var source = new TrainSubInventorySource(new TrainCarInstanceId(7), _trainCarViewRegistry);

            var model = source.CreateModel(response);

            Assert.AreEqual(TrainInventoryMessageType.ContainerMissing, source.LastOpenMessage);
            Assert.AreEqual(0, model.Count);
        }

        [TestCase(InventoryRequestResult.Timeout)]
        [TestCase(InventoryRequestResult.DeserializeFailed)]
        public void 通信失敗は両ソースの表示モデルへ残る(InventoryRequestResult result)
        {
            var source = new TrainSubInventorySource(new TrainCarInstanceId(7), _trainCarViewRegistry);
            var response = new InventoryResponse(source.InventoryIdentifier, new List<IItemStack>(), new List<FluidMessagePack>(), result);
            var model = source.CreateModel(response);
            Assert.AreEqual(result, model.Result);
            Assert.AreEqual(TrainInventoryMessageType.OpenFailed, source.LastOpenMessage);
            var block = new BlockSubInventorySource(null, Vector3Int.zero);
            var blockModel = block.CreateModel(response);
            Assert.AreEqual(result, blockModel.Result);
            Assert.AreEqual(0, blockModel.Count);
        }

        [Test]
        public void 列車ソースは成功応答のアイテムをそのまま載せる()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() };
            var source = new TrainSubInventorySource(new TrainCarInstanceId(7), _trainCarViewRegistry);

            var model = source.CreateModel(new InventoryResponse(identifier, items, new List<FluidMessagePack>(), InventoryRequestResult.Success));

            Assert.IsNull(source.LastOpenMessage);
            Assert.AreEqual(2, model.Count);
        }

        [Test]
        public void 列車ソースは失敗のあとの成功応答でエラー種別を落とす()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty() };
            var source = new TrainSubInventorySource(new TrainCarInstanceId(7), _trainCarViewRegistry);
            source.CreateModel(new InventoryResponse(identifier, new List<IItemStack>(), new List<FluidMessagePack>(), InventoryRequestResult.ContainerNotFound));

            var model = source.CreateModel(new InventoryResponse(identifier, items, new List<FluidMessagePack>(), InventoryRequestResult.Success));

            Assert.IsNull(source.LastOpenMessage);
            Assert.AreEqual(1, model.Count);
        }

        [Test]
        public void 貨車の液体の応答はモデルに載り表示の液体枠へ写る()
        {
            var water = MasterHolder.FluidMaster.GetFluidId(System.Guid.Parse("00000000-0000-0000-1234-000000000001"));
            var source = new TrainSubInventorySource(new TrainCarInstanceId(1), _trainCarViewRegistry);
            var response = new InventoryResponse(source.InventoryIdentifier, new List<IItemStack>(), new List<FluidMessagePack> { new(water, 300, 1000) }, InventoryRequestResult.Success);

            var model = source.CreateModel(response);
            Assert.AreEqual(1, model.Fluids.Count);
            Assert.IsNull(source.LastOpenMessage);

            // 表示の液体枠は応答の水・量・容量をそのまま持つ
            // The display fluid slot carries the response's water, amount and capacity as-is
            var slots = TrainCarFluidConversion.ToFluids(model.Fluids);
            Assert.AreEqual(1, slots.Count);
            var filled = (FilledBlockFluidSlotState)slots[0];
            Assert.AreEqual(water, filled.FluidId);
            Assert.AreEqual(300, filled.Amount, 0.001);
            Assert.AreEqual(1000, filled.Capacity, 0.001);
        }
    }
}
