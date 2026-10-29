using System;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.PlayerInventory.Interface.Subscription;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Protocol;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using UnityEngine.TestTools;
using static Server.Protocol.PacketResponse.InventoryItemMoveProtocol;

namespace Tests.CombinedTest.Game.InventoryMove
{
    // 移動拒否時の訂正イベントを検証する
    // Verifies that both ends of a rejected move are corrected to their current values, to the requester only, via the existing update events
    public class InventoryItemMoveRejectionCorrectionTest
    {
        private const int PlayerId = 1;
        private const int OtherPlayerId = 2;
        private static readonly Vector3Int MachinePosition = Vector3Int.one;

        [Test]
        public void RejectedMainToBlockMoveResendsBothSlotsToRequesterOnly()
        {
            var setup = Setup();
            setup.Main.SetItem(0, Stack(setup.UnboundItemId, 3));
            DrainSinks(setup);

            SendMove(setup.Packet, 3, InventoryIdentifierMessagePack.CreateMainMessage(), 0, BlockIdentifier(), 0);

            // 要求者だけに元の値が届く
            // The requester receives the original stack and the empty machine slot; nothing leaks to another player
            var events = setup.RequesterSink.TakeAll();
            InventoryMoveCorrectionEvents.AssertMainUpdate(events, 0, setup.UnboundItemId, 3);
            InventoryMoveCorrectionEvents.AssertBlockUpdate(events, MachinePosition, 0, ItemMaster.EmptyItemId, 0);
            Assert.IsEmpty(InventoryMoveCorrectionEvents.SlotUpdateEvents(setup.OtherSink.TakeAll()), "訂正は要求者以外へ送らない");
        }

        [Test]
        public void RejectedGrabToBlockMoveResendsGrabAndBlockSlot()
        {
            var setup = Setup();
            setup.Grab.SetItem(0, Stack(setup.UnboundItemId, 10));
            DrainSinks(setup);

            SendMove(setup.Packet, 10, InventoryIdentifierMessagePack.CreateGrabMessage(), 0, BlockIdentifier(), 0);

            var events = setup.RequesterSink.TakeAll();
            var grab = events.Where(e => e.Tag == GrabInventoryUpdateEventPacket.EventTag).Select(e => MessagePackSerializer.Deserialize<GrabInventoryUpdateEventMessagePack>(e.Payload)).ToList();
            Assert.AreEqual(1, grab.Count, "掴みの訂正は1件");
            Assert.AreEqual(setup.UnboundItemId, grab[0].Item.Id);
            Assert.AreEqual(10, grab[0].Item.Count);
            InventoryMoveCorrectionEvents.AssertBlockUpdate(events, MachinePosition, 0, ItemMaster.EmptyItemId, 0);
        }

        [Test]
        public void RejectedPartialSwapFromBlockResendsBlockAndMainSlot()
        {
            var setup = Setup();
            setup.Block.InsertItem(Stack(setup.BoundItemId, 2));
            setup.Main.SetItem(0, Stack(setup.UnboundItemId, 3));
            DrainSinks(setup);

            SendMove(setup.Packet, 1, BlockIdentifier(), 0, InventoryIdentifierMessagePack.CreateMainMessage(), 0);

            var events = setup.RequesterSink.TakeAll();
            InventoryMoveCorrectionEvents.AssertBlockUpdate(events, MachinePosition, 0, setup.BoundItemId, 2);
            InventoryMoveCorrectionEvents.AssertMainUpdate(events, 0, setup.UnboundItemId, 3);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void UnresolvedMoveNotifiesRequesterAndCorrectsResolvedSideOnly(bool fromResolved, bool toResolved)
        {
            var setup = Setup();
            setup.Main.SetItem(0, Stack(setup.UnboundItemId, 3));
            DrainSinks(setup);

            // 欠損元・欠損先・双方欠損を同じ拒否経路で検証
            // Verify missing source, destination, and both through the same rejection path
            var missing = InventoryIdentifierMessagePack.CreateBlockMessage(new Vector3Int(50, 0, 50));
            var main = InventoryIdentifierMessagePack.CreateMainMessage();
            LogAssert.Expect(LogType.Warning, new Regex($"Move rejected: result=UnresolvedInventory .*fromResolved={fromResolved}.*toResolved={toResolved}"));
            SendMove(setup.Packet, 3, fromResolved ? main : missing, 0, toResolved ? main : missing, 0);

            var events = setup.RequesterSink.TakeAll();
            InventoryMoveCorrectionEvents.AssertUnresolvedNotification(events);
            Assert.AreEqual(fromResolved || toResolved ? 1 : 0, InventoryMoveCorrectionEvents.SlotUpdateEvents(events).Count);
            if (fromResolved || toResolved) InventoryMoveCorrectionEvents.AssertMainUpdate(events, 0, setup.UnboundItemId, 3);
            Assert.IsFalse(events.Any(e => e.Tag == UnifiedInventoryEventPacket.EventTag), "存在しないブロックの訂正は送らない");
            Assert.IsEmpty(setup.OtherSink.TakeAll(), "通知と訂正は要求者以外へ送らない");
            Assert.AreEqual(Stack(setup.UnboundItemId, 3), setup.Main.GetItem(0), "拒否でサーバー在庫は変わらない");
        }

        [Test]
        public void SuccessfulMoveSendsNoExtraCorrection()
        {
            var setup = Setup();
            setup.Main.SetItem(0, Stack(setup.BoundItemId, 3));
            DrainSinks(setup);

            SendMove(setup.Packet, 3, InventoryIdentifierMessagePack.CreateMainMessage(), 0, BlockIdentifier(), 0);

            // 成功時は訂正の重複送信が無い
            // On success only the regular change events fire, with no duplicate for the same slot
            var events = setup.RequesterSink.TakeAll();
            Assert.AreEqual(1, events.Count(e => e.Tag == MainInventoryUpdateEventPacket.EventTag));
            InventoryMoveCorrectionEvents.AssertMainUpdate(events, 0, ItemMaster.EmptyItemId, 0);
        }

        #region Setup

        private readonly struct CorrectionSetup
        {
            public readonly PacketResponseCreator Packet;
            public readonly VanillaMachineBlockInventoryComponent Block;
            public readonly IOpenableInventory Main;
            public readonly IOpenableInventory Grab;
            public readonly CapturedEventSink RequesterSink;
            public readonly CapturedEventSink OtherSink;
            public readonly ItemId BoundItemId;
            public readonly ItemId UnboundItemId;

            public CorrectionSetup(PacketResponseCreator packet, VanillaMachineBlockInventoryComponent block, IOpenableInventory main, IOpenableInventory grab, CapturedEventSink requesterSink, CapturedEventSink otherSink, ItemId boundItemId, ItemId unboundItemId)
            {
                Packet = packet;
                Block = block;
                Main = main;
                Grab = grab;
                RequesterSink = requesterSink;
                OtherSink = otherSink;
                BoundItemId = boundItemId;
                UnboundItemId = unboundItemId;
            }
        }

        // 機械を置き2人分のsinkを登録する
        // Place a recipe-selected machine and register sinks for the requester and another player (who subscribes to the same machine)
        private static CorrectionSetup Setup()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => 0 < r.InputItems.Length && 0 < r.OutputItems.Length);
            var boundItemId = MasterHolder.ItemMaster.GetItemId(recipe.InputItems[0].ItemGuid);
            var unboundItemId = MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid);
            Assert.AreNotEqual(boundItemId, unboundItemId, "テスト前提: 入力素材と出力生産物は別アイテムであること");

            ServerContext.WorldBlockDatastore.TryAddBlock(MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid), MachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block);
            MachineRecipeSelectTestUtil.SelectRecipe(block, recipe);
            var inventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var requesterSink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var otherSink = EventTestUtil.RegisterCaptureSink(serviceProvider, OtherPlayerId);
            serviceProvider.GetService<IInventorySubscriptionStore>().Subscribe(OtherPlayerId, new BlockInventorySubInventoryIdentifier(MachinePosition));

            return new CorrectionSetup(packet, block.GetComponent<VanillaMachineBlockInventoryComponent>(), inventoryData.MainOpenableInventory, inventoryData.GrabInventory, requesterSink, otherSink, boundItemId, unboundItemId);
        }

        private static void DrainSinks(CorrectionSetup setup)
        {
            setup.RequesterSink.TakeAll();
            setup.OtherSink.TakeAll();
        }

        private static IItemStack Stack(ItemId itemId, int count)
        {
            return ServerContext.ItemStackFactory.Create(itemId, count);
        }

        private static InventoryIdentifierMessagePack BlockIdentifier()
        {
            return InventoryIdentifierMessagePack.CreateBlockMessage(MachinePosition);
        }

        private static void SendMove(PacketResponseCreator packet, int count, InventoryIdentifierMessagePack from, int fromSlot, InventoryIdentifierMessagePack to, int toSlot)
        {
            var payload = MessagePackSerializer.Serialize(new InventoryItemMoveProtocolMessagePack(count, ItemMoveType.SwapSlot, from, fromSlot, to, toSlot));
            packet.GetPacketResponse(payload, Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
        }

        #endregion
    }
}
