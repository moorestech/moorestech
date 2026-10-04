using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Tests.Util.PlayerIdentity;
using UnityEngine;
using static Server.Protocol.PacketResponse.GearChainConnectionEditProtocol;

namespace Tests.CombinedTest.Server.PacketTest.GearChain
{
    // 本物のプレイヤーと通知購読を用意する
    // Prepare real players and notification subscriptions
    internal sealed class GearChainEditTestWorld
    {
        internal static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        internal static readonly Vector3Int PosA = new(1, 0, 0);
        internal static readonly Vector3Int PosB = new(3, 0, 0);
        internal readonly IOpenableInventory Inventory;
        internal readonly ItemId ChainItemId;
        internal readonly ItemId FillerItemId;
        internal readonly CapturedEventSink RequesterSink;
        internal readonly CapturedEventSink OtherSink;
        private readonly PacketResponseCreator _packet;
        private readonly int _playerId;

        internal GearChainEditTestWorld(bool unlockTool)
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _packet = packet;
            // インベントリと通知先を別々の認証済みプレイヤーへ結び付ける
            // Bind inventory and notification recipients to distinct registered players
            var registry = provider.GetRequiredService<PlayerIdentityRegistry>();
            _playerId = PlayerIdentityTestHelper.Register(registry, "steam:1").PlayerId;
            var otherPlayerId = PlayerIdentityTestHelper.Register(registry, "steam:2").PlayerId;
            RequesterSink = EventTestUtil.RegisterCaptureSink(provider, _playerId);
            OtherSink = EventTestUtil.RegisterCaptureSink(provider, otherPlayerId);
            Inventory = provider.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(_playerId).MainOpenableInventory;
            ChainItemId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000004"));
            FillerItemId = MasterHolder.ItemMaster.GetItemId(Guid.Parse("00000000-0000-0000-1234-000000000001"));

            // 正規のマスタとポールを使って接続可否を判定させる
            // Use real masters and poles for connection evaluation
            if (unlockTool) provider.GetRequiredService<IGameUnlockStateDataController>().UnlockConnectTool(ChainToolGuid);
            PlacePole(PosA);
            PlacePole(PosB);
            ClearEvents();
        }

        internal void Connect()
        {
            Inventory.SetItem(0, ServerContext.ItemStackFactory.Create(ChainItemId, 10));
            var response = Send(GearChainConnectionEditRequest.CreateConnectRequest(PosA, PosB, ChainToolGuid));
            Assert.IsTrue(response.IsSuccess, response.Error);
            Assert.AreEqual(0, CountItem(ChainItemId));
            ClearEvents();
        }

        internal GearChainConnectionEditResponse Send(GearChainConnectionEditRequest request)
        {
            var payload = MessagePackSerializer.Serialize(request);
            var bytes = _packet.GetPacketResponse(payload, BoundPacketContext.Bind(_playerId)).Single();
            return MessagePackSerializer.Deserialize<GearChainConnectionEditResponse>(bytes.ToArray());
        }

        internal void AssertDenied(GearChainConnectionEditResponse response, string reason, string messageId)
        {
            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual(reason, response.Error);
            var denied = TakeDenied(RequesterSink);
            Assert.AreEqual(1, denied.Count);
            Assert.AreEqual(messageId, denied.Single().MessageId);
            Assert.IsEmpty(TakeDenied(OtherSink), "Refusals must reach only the requester");
        }

        internal void FillInventory()
        {
            var max = ItemStackLevelDataStore.Instance.GetMaxStack(FillerItemId);
            for (var slot = 0; slot < Inventory.GetSlotSize(); slot++)
            {
                Inventory.SetItem(slot, ServerContext.ItemStackFactory.Create(FillerItemId, max));
            }
        }

        internal int CountItem(ItemId itemId)
        {
            return Inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }

        internal static IGearChainPole Pole(Vector3Int position)
        {
            return ServerContext.WorldBlockDatastore.GetBlock(position).GetComponent<IGearChainPole>();
        }

        internal static void PlacePole(Vector3Int position)
        {
            Assert.IsTrue(ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, position, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
        }

        internal static List<NotificationMessagePack> TakeDenied(CapturedEventSink sink)
        {
            return sink.TakeAll().Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(n => n.Category == NotificationCategory.OperationDenied).ToList();
        }

        internal void ClearEvents()
        {
            RequesterSink.TakeAll();
            OtherSink.TakeAll();
        }
    }
}
