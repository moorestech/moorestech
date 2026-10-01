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
    public abstract class ItemEarnedNotificationTestBase
    {
        protected const int PlayerId = 1;

        // 素手一撃で破壊できるPickUp型mapObject
        // A PickUp-type mapObject destroyed by a single bare-handed hit
        protected static readonly Guid PickUpMapObjectGuid = Guid.Parse("8c0e1339-be75-4690-99cd-58b5385a17cd");
        protected static readonly Guid IronVeinGuid = Guid.Parse("11111111-0000-0000-0000-000000000001");
        protected static readonly Vector3Int InsideIronVein = new(0, 5, 0);
        protected static readonly Guid ToolItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");

        // 空きを潰すための獲得アイテムとは別のアイテム
        // A different item used to fill the inventory
        protected static readonly Guid FillerItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        protected const double ExpectedAttackSpeed = 0.2;

        protected void SendMapObjectMining(PacketResponseCreator packet, int instanceId)
        {
            var messagePack = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(instanceId);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
        }

        protected void SendVeinMining(PacketResponseCreator packet)
        {
            var messagePack = MiningProtocol.MiningProtocolMessagePack.CreateVeinRequest(IronVeinGuid, InsideIronVein);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
        }

        // 指定アイテムの空きだけを残して他スロットを別アイテムで埋める
        // Fills every other slot with a different item, leaving free space only for the given item
        protected void FillInventoryLeavingFreeSpace(PlayerInventoryData playerInventory, ItemId itemId, int freeSpace)
        {
            var mainInventory = playerInventory.MainOpenableInventory;
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(itemId);
            Assert.Greater(maxStack, freeSpace);
            mainInventory.SetItem(0, itemId, maxStack - freeSpace);

            var fillerItemId = MasterHolder.ItemMaster.GetItemId(FillerItemGuid);
            var fillerMaxStack = ItemStackLevelDataStore.Instance.GetMaxStack(fillerItemId);
            for (var slot = 1; slot < mainInventory.GetSlotSize(); slot++) mainInventory.SetItem(slot, fillerItemId, fillerMaxStack);
        }

        protected void EquipTool(PlayerInventoryData playerInventory)
        {
            playerInventory.EquipmentInventory.SetItem(0, MasterHolder.ItemMaster.GetItemId(ToolItemGuid), 1);
            playerInventory.EquipmentInventory.SetSelectedEquipmentIndex(0);
        }

        protected System.Collections.Generic.List<NotificationMessagePack> TakeItemEarnedNotifications(CapturedEventSink sink)
        {
            return TakeNotifications(sink, NotificationCategory.ItemEarned);
        }

        protected System.Collections.Generic.List<NotificationMessagePack> TakeNotifications(CapturedEventSink sink, NotificationCategory category)
        {
            return sink.TakeAll().
                Where(captured => captured.Tag == NotificationService.EventTag).
                Select(captured => MessagePackSerializer.Deserialize<NotificationMessagePack>(captured.Payload)).
                Where(notification => notification.Category == category).
                ToList();
        }

        /// <summary>
        ///     採掘設定は判別子の内側にあるため、採掘できる個体としてほどいてから読む
        ///     The mining settings live inside the discriminator, so they are unwrapped as a minable object first
        /// </summary>
        protected static IMinableMapObjectParam GetMinableParam(Guid mapObjectGuid)
        {
            return (IMinableMapObjectParam)MasterHolder.MapObjectMaster.GetMapObjectElement(mapObjectGuid).MiningParam;
        }

        protected int CountMainInventoryItem(ServiceProvider serviceProvider, ItemId itemId)
        {
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            return Enumerable.Range(0, mainInventory.GetSlotSize()).
                Where(slot => mainInventory.GetItem(slot).Id == itemId).
                Sum(slot => mainInventory.GetItem(slot).Count);
        }
    }
}
