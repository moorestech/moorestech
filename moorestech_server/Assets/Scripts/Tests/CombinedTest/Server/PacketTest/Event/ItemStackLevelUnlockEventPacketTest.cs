using System.Linq;
using Game.Map.Interface.Json;
using Game.Research;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.ItemStackLevelUpgradeTest;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ItemStackLevelUnlockEventPacketTest
    {
        // 研究完了によるスタックレベル解放がイベントとして配信される
        // Stack level unlocks from research completion are broadcast as events
        [Test]
        public void UnlockStackLevelToEventPacketTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // イベントがないことを確認する
            // Verify no events yet
            Assert.AreEqual(0, sink.TakeAll().Count);

            // スタックレベル解放アクション付きの研究を完了させる
            // Complete the research that carries an unlockItemStackLevel action
            serviceProvider.GetService<IResearchDataStore>().CompleteResearch(StackUpgradeResearchGuid, PlayerId);

            // イベントを受け取り、内容を検証する
            // Receive the event and verify the payload
            var events = sink.TakeAll();

            var stackLevelEvents = events.Where(e => e.Tag == ItemStackLevelUnlockEventPacket.EventTag).ToList();
            Assert.AreEqual(1, stackLevelEvents.Count);

            var eventData = MessagePackSerializer.Deserialize<ItemStackLevelUnlockEventPacket.ItemStackLevelMessagePack>(stackLevelEvents[0].Payload);
            Assert.AreEqual(Test1ItemGuid, eventData.ItemGuid);
            Assert.AreEqual(2, eventData.Level);
        }

        // 解放済みスタックレベルが初期ハンドシェイクに同梱される
        // Unlocked stack levels are bundled into the initial handshake response
        [Test]
        public void UnlockedLevelsAreIncludedInInitialHandshakeTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize(serviceProvider.GetService<MapInfoJson>());
            serviceProvider.GetService<IResearchDataStore>().CompleteResearch(StackUpgradeResearchGuid, PlayerId);

            var handshakeRequest = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(PlayerId, "test player"));
            var response = packetResponse.GetPacketResponse(handshakeRequest, new PacketResponseContext(null))[0];
            var handshakeResponse = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(response);

            Assert.AreEqual(1, handshakeResponse.ItemStackLevels.Length);
            Assert.AreEqual(Test1ItemGuid, handshakeResponse.ItemStackLevels[0].ItemGuid);
            Assert.AreEqual(2, handshakeResponse.ItemStackLevels[0].Level);
        }
    }
}
