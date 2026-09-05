using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory.Train;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Core.Item.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;

namespace Client.Tests.UIState.Models
{
    public class SubInventorySourceModelTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 列車ソースはコンテナ無し応答をエラー種別へ写す()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var response = new InventoryResponse(identifier, new List<IItemStack>(), InventoryRequestResult.ContainerNotFound);
            var source = new TrainSubInventorySource(7);

            var model = source.CreateModel(response);

            Assert.AreEqual(TrainInventoryMessageType.ContainerMissing, source.LastOpenMessage);
            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void 列車ソースは成功応答のアイテムをそのまま載せる()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() };
            var source = new TrainSubInventorySource(7);

            var model = source.CreateModel(new InventoryResponse(identifier, items, InventoryRequestResult.Success));

            Assert.IsNull(source.LastOpenMessage);
            Assert.AreEqual(2, model.Count);
        }

        [Test]
        public void 列車ソースは失敗のあとの成功応答でエラー種別を落とす()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty() };
            var source = new TrainSubInventorySource(7);
            source.CreateModel(new InventoryResponse(identifier, new List<IItemStack>(), InventoryRequestResult.ContainerNotFound));

            var model = source.CreateModel(new InventoryResponse(identifier, items, InventoryRequestResult.Success));

            Assert.IsNull(source.LastOpenMessage);
            Assert.AreEqual(1, model.Count);
        }
    }
}
