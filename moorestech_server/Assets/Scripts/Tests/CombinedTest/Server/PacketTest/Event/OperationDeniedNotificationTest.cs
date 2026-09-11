using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Tests.CombinedTest.Game.ResearchDataStoreTest;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class OperationDeniedNotificationTest
    {
        [Test]
        public void CompleteResearchFailureFiresDeniedNotification()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 素材ゼロで研究完了要求→拒否通知
            // Request research completion with no materials → denied notification
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(PlayerId, Research1Guid);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.researchNotCompletable"));
        }

        [Test]
        public void CompleteResearchAlreadyCompletedDoesNotFireDeniedNotification()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 正規手順で研究完了→シンクをクリア
            // Complete research legitimately → drain the sink
            CompleteResearchForTest(serviceProvider, Research1Guid);
            sink.TakeAll();

            // 完了済み研究への二重完了要求
            // Send a duplicate completion request for the same guid
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(PlayerId, Research1Guid);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(0, denied.Count(d => d.MessageId == "denied.researchNotCompletable"));
        }

        [Test]
        public void OneClickCraftMaterialShortageFiresDeniedNotification()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // 素材ゼロでクラフト要求 → 素材不足通知
            // Request crafting with no materials → material shortage notification
            var recipeGuid = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[0].CraftRecipeGuid;
            var request = new OneClickCraft.RequestOneClickCraftProtocolMessagePack(PlayerId, recipeGuid);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.craftMaterialShortage"));
        }

        [Test]
        public void PlaceBlockNotUnlockedFiresDeniedNotification()
        {
            var (packet, serviceProvider) = PlaceBlockProtocolTestSupport.CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlaceBlockProtocolTestSupport.PlayerId);

            // 未解放ブロック設置→未解放通知
            // Request placing a locked block → not-unlocked notification
            var blockId = Tests.Module.TestMod.ForUnitTestModBlockId.LockedElectricPoleId;
            var payload = PlaceBlockProtocolTestSupport.CreatePlaceBlockPayload(blockId, (0, 0));
            packet.GetPacketResponse(payload, new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.placeBlockNotUnlocked"));
        }

        [Test]
        public void BeltReplaceOnNonBeltFiresDeniedNotification()
        {
            var (packet, serviceProvider) = PlaceBlockProtocolTestSupport.CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlaceBlockProtocolTestSupport.PlayerId);
            var pos = new Vector3Int(80, 0, 80);

            // ベルト以外へ張替え→拒否
            // Replacing a non-belt block → rejected
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.SmallGearBeltConveyor);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var payload = PlaceBlockProtocolTestSupport.CreateReplacePayload(ForUnitTestModBlockId.SmallGearBeltConveyor, pos, BlockDirection.North);
            packet.GetPacketResponse(payload, new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.placeBlockReplaceRejected"));
        }

        [Test]
        public void BeltReplaceWithFullInventoryFiresDeniedNotification()
        {
            var (packet, serviceProvider) = PlaceBlockProtocolTestSupport.CreateServer();
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlaceBlockProtocolTestSupport.PlayerId);
            var pos = new Vector3Int(82, 0, 82);

            // 返却先無しで張替え→満杯
            // Replacing with no room for the returned items → inventory-full
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.BeltConveyorId);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var oldBlock);
            oldBlock.GetComponent<VanillaBeltConveyorComponent>().InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1), InsertItemContext.Empty);
            PlaceBlockProtocolTestSupport.OccupyAllInventorySlots(serviceProvider, ForUnitTestItemId.ItemId1);
            var payload = PlaceBlockProtocolTestSupport.CreateReplacePayload(ForUnitTestModBlockId.BeltConveyorId, pos, BlockDirection.North);
            packet.GetPacketResponse(payload, new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.placeBlockReplaceInventoryFull"));
        }

        [Test]
        public void RailConnectInvalidNodeFiresDeniedNotification()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var sink = EventTestUtil.RegisterCaptureSink(environment.ServiceProvider, PlayerId);

            // 存在しないノード接続→拒否通知
            // Request connecting nonexistent nodes → denied notification
            var request = RailConnectionEditProtocol.RailConnectionEditRequest.CreateConnectRequest(
                PlayerId, 999999, Guid.NewGuid(), 999998, Guid.NewGuid(), Guid.NewGuid());
            environment.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.railEdit.InvalidNode"));
        }

        private static List<NotificationMessagePack> TakeDenied(CapturedEventSink sink)
        {
            return sink.TakeAll()
                .Where(e => e.Tag == NotificationService.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<NotificationMessagePack>(e.Payload))
                .Where(n => n.Category == NotificationCategory.OperationDenied)
                .ToList();
        }
    }
}
