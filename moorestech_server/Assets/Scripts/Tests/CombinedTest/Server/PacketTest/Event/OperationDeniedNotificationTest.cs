using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.Notification;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest;
using Tests.Module.TestMod;
using Tests.Util;
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
