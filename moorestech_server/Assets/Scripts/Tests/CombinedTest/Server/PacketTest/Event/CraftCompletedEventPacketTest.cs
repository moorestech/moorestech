using System.Linq;
using Core.Master;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.OneClickCraft;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class CraftCompletedEventPacketTest
    {
        private const int PlayerId = 0;
        private const int OtherPlayerId = 1;
        private const int CraftRecipeId = 1;

        // 成立したクラフトだけが、作った本人へレシピ付きで1件届く
        // Only a craft that went through reaches its own crafter, once, with the recipe
        [Test]
        public void クラフトが成立すると本人にだけレシピ付きのイベントが届く()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var otherSink = EventTestUtil.RegisterCaptureSink(serviceProvider, OtherPlayerId);

            ChallengeCompletedEventTest.ClearCraftChallenge(packet, serviceProvider);

            var craftEvents = sink.TakeAll().Where(e => e.Tag == CraftCompletedEventPacket.EventTag).ToList();
            Assert.AreEqual(1, craftEvents.Count);
            var data = MessagePackSerializer.Deserialize<CraftCompletedEventPacket.CraftCompletedEventMessagePack>(craftEvents[0].Payload);
            Assert.AreEqual(PlayerId, data.PlayerId);
            Assert.AreEqual(MasterHolder.CraftRecipeMaster.CraftRecipes.Data[CraftRecipeId].CraftRecipeGuid, data.CraftRecipeGuid);

            // 他プレイヤーのクラフト数へ混ざらない
            // It never leaks into another player's craft count
            Assert.IsFalse(otherSink.TakeAll().Any(e => e.Tag == CraftCompletedEventPacket.EventTag));
        }

        // 素材不足で拒否された要求は成立ではないので届かない。届くと craftCount が押した回数に化ける
        // A request rejected for missing materials is not a craft and sends nothing; otherwise craftCount turns into times clicked
        [Test]
        public void 素材不足で拒否されたクラフトはイベントを出さない()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            var craftRecipeGuid = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[CraftRecipeId].CraftRecipeGuid;
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, craftRecipeGuid)), new PacketResponseContext(null));

            Assert.IsFalse(sink.TakeAll().Any(e => e.Tag == CraftCompletedEventPacket.EventTag));
        }
    }
}
