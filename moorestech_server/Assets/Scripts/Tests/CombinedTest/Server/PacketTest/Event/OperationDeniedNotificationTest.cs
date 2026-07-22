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

            // 素材ゼロの初期状態で研究完了を要求 → 失敗して拒否通知が飛ぶ
            // Request research completion with an empty inventory → fails and fires a denied notification
            var request = new CompleteResearchProtocol.RequestCompleteResearchMessagePack(PlayerId, Research1Guid);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.researchNotCompletable"));
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

            // 初期ロック状態のブロックを設置要求 → スキップされ未解放通知が1件飛ぶ
            // Request placing a block that starts locked → the cell is skipped and one not-unlocked notification fires
            var blockId = Tests.Module.TestMod.ForUnitTestModBlockId.LockedElectricPoleId;
            var payload = PlaceBlockProtocolTestSupport.CreatePlaceBlockPayload(blockId, (0, 0));
            packet.GetPacketResponse(payload, new PacketResponseContext(null));

            var denied = TakeDenied(sink);
            Assert.AreEqual(1, denied.Count(d => d.MessageId == "denied.placeBlockNotUnlocked"));
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
