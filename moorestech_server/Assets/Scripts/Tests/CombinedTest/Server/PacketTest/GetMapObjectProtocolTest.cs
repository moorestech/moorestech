using System.Linq;
using Core.Item;
using Game.MapObject.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetMapObjectProtocolTest
    {
        private const int PlayerId = 0;

        [Test]
        public void GetMapObjectProtocol_DestroyAndAddToInventory_Test()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);

            var playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var worldMapObjectDataStore = serviceProvider.GetService<IMapObjectDatastore>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();


            //マップオブジェクトを取得するプロトコルを送信
            var mapObject = worldMapObjectDataStore.MapObjects[0];
            packet.GetPacketResponse(MessagePackSerializer
                .Serialize(new GetMapObjectProtocolProtocolMessagePack(PlayerId, mapObject.InstanceId)).ToList());


            //実際マップオブジェクトが取得されているかのテスト
            var mapObjectItemStack = itemStackFactory.Create(mapObject.ItemId, mapObject.ItemCount);
            var playerInventory = playerInventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            Assert.AreEqual(playerInventory.GetItem(0), mapObjectItemStack);
        }
    }
}