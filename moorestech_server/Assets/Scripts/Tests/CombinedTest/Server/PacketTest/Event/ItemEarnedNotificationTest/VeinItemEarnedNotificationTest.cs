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
    public class VeinItemEarnedNotificationTest : ItemEarnedNotificationTestBase
    {

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
    }
}
