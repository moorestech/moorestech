using System.Linq;
using Game.Context;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.MapObjectAcquisitionProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetMapObjectProtocolTest
    {
        private const int PlayerId = 0;
        
        [Test]
        public void GetMapObjectProtocol_DestroyAndAddToInventory_Test()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var itemFactory = ServerContext.ItemStackFactory;
            
            var mapObject = ServerContext.MapObjectDatastore.MapObjects[0];
            
            var playerInventory = playerInventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            var itemSlot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            
            
            // 少ないダメージでアイテムが入手できないことのテスト
            var messagePack = new GetMapObjectProtocolProtocolMessagePack(PlayerId, mapObject.InstanceId, 5);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList());
            
            Assert.AreEqual(itemFactory.CreatEmpty(), playerInventory.GetItem(itemSlot));
            
            
            // アイテムがもらえるだけのダメージを与えてアイテムを入手できることのテスト
            messagePack = new GetMapObjectProtocolProtocolMessagePack(PlayerId, mapObject.InstanceId, 5);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList());
            
            var earnItem = mapObject.EarnItems[0];
            Assert.AreEqual(earnItem, playerInventory.GetItem(itemSlot));
            playerInventory.SetItem(itemSlot, itemFactory.CreatEmpty()); // アイテムをリセット
            
            
            //大きくダメージを与えて2倍のアイテムを入手できることのテスト
            messagePack = new GetMapObjectProtocolProtocolMessagePack(PlayerId, mapObject.InstanceId, 20);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList());
            
            Assert.AreEqual(earnItem.Id, playerInventory.GetItem(itemSlot).Id);
            Assert.AreEqual(earnItem.Count * 2, playerInventory.GetItem(itemSlot).Count);
            
            //破壊されていることのテスト
            Assert.IsTrue(mapObject.IsDestroyed);
        }
    }
}