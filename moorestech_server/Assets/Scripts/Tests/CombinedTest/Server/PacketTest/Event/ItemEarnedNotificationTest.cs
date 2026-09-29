using System;
using System.Linq;
using Core.Item;
using Core.Master;
using Core.Update;
using Game.Context;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.MapModule;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     手掘りの獲得はItemEarned通知で飛ぶ
    ///     Verifies that hand-mining rewards are pushed as ItemEarned notifications
    /// </summary>
    public class ItemEarnedNotificationTest : ItemEarnedNotificationTestBase
    {

        [Test]
        public void MapObject採掘の獲得はアイテムごとに1本の通知として飛ぶ()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var mapObject = ServerContext.MapObjectDatastore.MapObjects.First(target => target.MapObjectGuid == PickUpMapObjectGuid);

            SendMapObjectMining(packet, mapObject.InstanceId);

            // 分割されても通知は1本、Countは総数
            // Even when the reward splits into several stacks the notification stays single and Count matches the total
            var notifications = TakeItemEarnedNotifications(sink);
            Assert.AreEqual(1, notifications.Count);

            var earnItem = GetMinableParam(PickUpMapObjectGuid).EarnItems.items[0];
            var earnItemId = MasterHolder.ItemMaster.GetItemId(earnItem.ItemGuid);
            Assert.AreEqual(earnItemId, notifications[0].ItemId);
            Assert.AreEqual(CountMainInventoryItem(serviceProvider, earnItemId), notifications[0].Count);
            Assert.AreEqual("itemEarned.mined", notifications[0].MessageId);
        }

        [Test]
        public void 溢れて入らなかった分は通知の個数に含めない()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = ServerContext.MapObjectDatastore.MapObjects.First(target => target.MapObjectGuid == PickUpMapObjectGuid);
            var earnItem = GetMinableParam(PickUpMapObjectGuid).EarnItems.items[0];
            var earnItemId = MasterHolder.ItemMaster.GetItemId(earnItem.ItemGuid);

            // MaxCount1周分+1の空きは通る
            // A space one above a MaxCount round passes the check
            var freeSpace = earnItem.MaxCount + 1;
            FillInventoryLeavingFreeSpace(playerInventory, earnItemId, freeSpace);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var beforeCount = CountMainInventoryItem(serviceProvider, earnItemId);

            // PickUpは全境界を跨ぎ溢れさせる
            // PickUp crosses every threshold and generates more than fits
            SendMapObjectMining(packet, mapObject.InstanceId);

            var notifications = TakeItemEarnedNotifications(sink);
            Assert.AreEqual(1, notifications.Count);
            Assert.AreEqual(freeSpace, CountMainInventoryItem(serviceProvider, earnItemId) - beforeCount);
            Assert.AreEqual(freeSpace, notifications[0].Count);
        }

        [Test]
        public void 満杯で失われた分は拒否通知として飛ぶ()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var mapObject = ServerContext.MapObjectDatastore.MapObjects.First(target => target.MapObjectGuid == PickUpMapObjectGuid);
            var earnItem = GetMinableParam(PickUpMapObjectGuid).EarnItems.items[0];
            var earnItemId = MasterHolder.ItemMaster.GetItemId(earnItem.ItemGuid);

            FillInventoryLeavingFreeSpace(playerInventory, earnItemId, earnItem.MaxCount + 1);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            SendMapObjectMining(packet, mapObject.InstanceId);

            // 溢れて消えた分は無言にせず拒否として届く
            // The overflow that vanished arrives as a denial instead of staying silent
            var denied = TakeNotifications(sink, NotificationCategory.OperationDenied);
            Assert.AreEqual(1, denied.Count);
            Assert.AreEqual("denied.miningInventoryFull", denied[0].MessageId);
        }

        [Test]
        public void 溢れなければ拒否通知は飛ばない()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var mapObject = ServerContext.MapObjectDatastore.MapObjects.First(target => target.MapObjectGuid == PickUpMapObjectGuid);

            SendMapObjectMining(packet, mapObject.InstanceId);

            Assert.AreEqual(0, TakeNotifications(sink, NotificationCategory.OperationDenied).Count);
        }
    }
}
