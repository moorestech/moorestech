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
    public class ItemEarnedNotificationTest
    {
        private const int PlayerId = 0;

        // 素手一撃で破壊できるPickUp型mapObject
        // A PickUp-type mapObject destroyed by a single bare-handed hit
        private static readonly Guid PickUpMapObjectGuid = Guid.Parse("8c0e1339-be75-4690-99cd-58b5385a17cd");
        private static readonly Guid IronVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        private static readonly Vector3Int InsideIronVein = new(0, 5, 0);
        private static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 空きを潰すための獲得アイテムとは別のアイテム
        // A different item used to fill the inventory
        private static readonly Guid FillerItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private const double ExpectedAttackSpeed = 0.2;

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

            var earnItem = MasterHolder.MapObjectMaster.GetMapObjectElement(PickUpMapObjectGuid).EarnItems[0];
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
            var earnItem = MasterHolder.MapObjectMaster.GetMapObjectElement(PickUpMapObjectGuid).EarnItems[0];
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
            var earnItem = MasterHolder.MapObjectMaster.GetMapObjectElement(PickUpMapObjectGuid).EarnItems[0];
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

        [Test]
        public void Vein手掘りの獲得も通知として飛ぶ()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            EquipTool(playerInventory);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            SendVeinMining(packet);

            // veinは1振り1ドロップでCount1
            // A vein drops one item per swing, so Count is 1
            var notifications = TakeItemEarnedNotifications(sink);
            Assert.AreEqual(1, notifications.Count);
            Assert.AreEqual(1, notifications[0].Count);

            var veinItemGuid = ((ItemVeinParam)MasterHolder.MapVeinMaster.GetElementOrNull(IronVeinGuid).VeinParam).ItemGuid;
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(veinItemGuid), notifications[0].ItemId);
        }

        [Test]
        public void 獲得通知はクールダウンで握り潰されず連続採掘のたびに飛ぶ()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            EquipTool(playerInventory);
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // クールダウン3秒内の2連打も通る
            // Two swings inside the 3s cooldown both reach the wire
            SendVeinMining(packet);
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(ExpectedAttackSpeed) + 1);
            SendVeinMining(packet);

            Assert.AreEqual(2, TakeItemEarnedNotifications(sink).Count);
        }

        private void SendMapObjectMining(PacketResponseCreator packet, int instanceId)
        {
            var messagePack = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(PlayerId, instanceId);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), new PacketResponseContext(null));
        }

        private void SendVeinMining(PacketResponseCreator packet)
        {
            var messagePack = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(PlayerId, IronVeinGuid, InsideIronVein);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), new PacketResponseContext(null));
        }

        // 指定アイテムの空きだけを残して他スロットを別アイテムで埋める
        // Fills every other slot with a different item, leaving free space only for the given item
        private void FillInventoryLeavingFreeSpace(PlayerInventoryData playerInventory, ItemId itemId, int freeSpace)
        {
            var mainInventory = playerInventory.MainOpenableInventory;
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
            Assert.Greater(maxStack, freeSpace);
            mainInventory.SetItem(0, itemId, maxStack - freeSpace);

            var fillerItemId = MasterHolder.ItemMaster.GetItemId(FillerItemGuid);
            var fillerMaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(fillerItemId);
            for (var slot = 1; slot < mainInventory.GetSlotSize(); slot++) mainInventory.SetItem(slot, fillerItemId, fillerMaxStack);
        }

        private void EquipTool(PlayerInventoryData playerInventory)
        {
            playerInventory.EquipmentInventory.SetItem(0, MasterHolder.ItemMaster.GetItemId(ToolItemGuid), 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }

        private System.Collections.Generic.List<NotificationMessagePack> TakeItemEarnedNotifications(CapturedEventSink sink)
        {
            return TakeNotifications(sink, NotificationCategory.ItemEarned);
        }

        private System.Collections.Generic.List<NotificationMessagePack> TakeNotifications(CapturedEventSink sink, NotificationCategory category)
        {
            return sink.TakeAll().
                Where(captured => captured.Tag == NotificationService.EventTag).
                Select(captured => MessagePackSerializer.Deserialize<NotificationMessagePack>(captured.Payload)).
                Where(notification => notification.Category == category).
                ToList();
        }

        private int CountMainInventoryItem(ServiceProvider serviceProvider, ItemId itemId)
        {
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            return Enumerable.Range(0, mainInventory.GetSlotSize()).
                Where(slot => mainInventory.GetItem(slot).Id == itemId).
                Sum(slot => mainInventory.GetItem(slot).Count);
        }
    }
}
