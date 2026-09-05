using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Core.Item.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;

namespace Client.Tests.UIState
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
            var source = new TrainSubInventorySourceForTest(7);

            var model = source.CreateModel(response);

            Assert.AreEqual(TrainInventoryMessageType.ContainerMissing, model.TrainMessage);
            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void 列車ソースは成功応答のアイテムをそのまま載せる()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() };
            var source = new TrainSubInventorySourceForTest(7);

            var model = source.CreateModel(new InventoryResponse(identifier, items, InventoryRequestResult.Success));

            Assert.IsNull(model.TrainMessage);
            Assert.AreEqual(2, model.Count);
        }

        // TrainCarEntityObject は MonoBehaviour なので識別子だけを差し替える最小の派生で組む
        // TrainCarEntityObject is a MonoBehaviour, so build the source with the minimal identifier-only derivation
        private class TrainSubInventorySourceForTest : TrainSubInventorySource
        {
            public TrainSubInventorySourceForTest(long trainCarInstanceId) : base(trainCarInstanceId) { }
        }
    }
}
