using System;
using System.Linq;
using Core.Master;
using Game.Context;
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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var itemFactory = ServerContext.ItemStackFactory;

            var mapObject = ServerContext.MapObjectDatastore.MapObjects[0];

            var playerInventory = playerInventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            var itemSlot = PlayerInventoryConst.HotBarSlotToInventorySlot(0);

            // 期待するアイテムIDをマスターデータから取得
            // Get expected item ID from master data
            var mapObjectConfig = MasterHolder.MapObjectMaster.GetMapObjectElement(mapObject.MapObjectGuid);
            var expectedItemGuid = mapObjectConfig.EarnItems[0].ItemGuid;
            var expectedItemId = MasterHolder.ItemMaster.GetItemId(expectedItemGuid);
            var minCount = mapObjectConfig.EarnItems[0].MinCount;
            var maxCount = mapObjectConfig.EarnItems[0].MaxCount;


            // 少ないダメージでアイテムが入手できないことのテスト
            // Test that small damage does not yield items
            var messagePack = new GetMapObjectProtocolProtocolMessagePack(PlayerId, mapObject.InstanceId, 5);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack));

            Assert.AreEqual(itemFactory.CreatEmpty(), playerInventory.GetItem(itemSlot));


            // アイテムがもらえるだけのダメージを与えてアイテムを入手できることのテスト
            // Test that sufficient damage yields items
            messagePack = new GetMapObjectProtocolProtocolMessagePack(PlayerId, mapObject.InstanceId, 5);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack));

            var obtainedItem = playerInventory.GetItem(itemSlot);
            Assert.AreEqual(expectedItemId, obtainedItem.Id);
            Assert.IsTrue(obtainedItem.Count >= minCount && obtainedItem.Count <= maxCount,
                $"Count should be between {minCount} and {maxCount}, but was {obtainedItem.Count}");
            playerInventory.SetItem(itemSlot, itemFactory.CreatEmpty()); // アイテムをリセット


            // 大きくダメージを与えて2回分のアイテムを入手できることのテスト
            // Test that large damage yields items for crossing 2 thresholds
            messagePack = new GetMapObjectProtocolProtocolMessagePack(PlayerId, mapObject.InstanceId, 20);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack));

            obtainedItem = playerInventory.GetItem(itemSlot);
            Assert.AreEqual(expectedItemId, obtainedItem.Id);
            Assert.IsTrue(obtainedItem.Count >= minCount * 2 && obtainedItem.Count <= maxCount * 2,
                $"Count should be between {minCount * 2} and {maxCount * 2}, but was {obtainedItem.Count}");

            // 破壊されていることのテスト
            // Test that object is destroyed
            Assert.IsTrue(mapObject.IsDestroyed);
        }
    }
}