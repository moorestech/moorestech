using System.Text.RegularExpressions;
using Game.PlayerConnection;
using Game.PlayerIdentity;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest.Handshake
{
    public class InitialHandshakeIdentityTest
    {
        private const string SteamA = "steam:76561198319362448";
        private PacketResponseCreator _packet;
        private ServiceProvider _provider;

        [SetUp]
        public void SetUp()
        {
            (_packet, _provider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void NewIdentityReceivesSequentialIdAndReconnectKeepsItTest()
        {
            // 最初の接続に1を払い出し、切断処理と同じ順で解除する
            // Assign id 1 to the first connection and unregister in the same order as cleanup
            var context = new PacketResponseContext(new CapturedEventSink());
            var first = Handshake(context, SteamA);
            Assert.AreEqual(HandshakeRejection.None, first.Rejection);
            Assert.AreEqual(1, first.Accepted.PlayerId);
            Assert.AreEqual(first.Accepted.PlayerId, context.PlayerId);
            var playerId = context.MarkClosedAndGetPlayerId().Value;
            _provider.GetRequiredService<EventProtocolProvider>().UnregisterPlayer(playerId, context.EventSink);
            _provider.GetRequiredService<PlayerConnectionRegistry>().Unregister(playerId);

            // 再接続では同じIDを返し、別の身元だけ次のIDを受け取る
            // Reconnection keeps the id; only another identity receives the next one
            var again = Handshake(new PacketResponseContext(new CapturedEventSink()), SteamA);
            Assert.AreEqual(HandshakeRejection.None, again.Rejection);
            Assert.AreEqual(first.Accepted.PlayerId, again.Accepted.PlayerId);
            var other = Handshake(new PacketResponseContext(new CapturedEventSink()), "steam:2");
            Assert.AreEqual(2, other.Accepted.PlayerId);
        }

        [Test]
        public void DuplicateIdentityDoesNotReplaceOriginalEventSinkTest()
        {
            var originalSink = new CapturedEventSink();
            var original = Handshake(new PacketResponseContext(originalSink), SteamA);
            originalSink.TakeAll();
            var duplicateSink = new CapturedEventSink();
            var duplicateContext = new PacketResponseContext(duplicateSink);

            // 後からの接続を拒否して、元の宛先だけを維持する
            // Reject the later connection and retain only the original destination
            LogAssert.Expect(LogType.Warning, new Regex("接続中"));
            var duplicate = Handshake(duplicateContext, SteamA);
            Assert.AreEqual(HandshakeRejection.AlreadyConnected, duplicate.Rejection);
            Assert.IsNull(duplicate.Accepted);
            Assert.IsNull(duplicateContext.PlayerId);
            _provider.GetRequiredService<EventProtocolProvider>().AddEvent(original.Accepted.PlayerId, "test:probe", new byte[0]);
            Assert.AreEqual(1, originalSink.Events.Count);
            Assert.IsEmpty(duplicateSink.Events);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1")]
        [TestCase("steam:12a")]
        public void InvalidIdentityDoesNotRegisterTest(string identity)
        {
            LogAssert.Expect(LogType.Warning, new Regex("身元"));
            var response = Handshake(new PacketResponseContext(new CapturedEventSink()), identity);
            Assert.AreEqual(HandshakeRejection.InvalidIdentity, response.Rejection);
            Assert.IsNull(response.Accepted);
            Assert.IsEmpty(_provider.GetRequiredService<PlayerIdentityRegistry>().GetSaveJsonObject().Entries);
        }

        [Test]
        public void BoundConnectionCannotSwitchIdentityTest()
        {
            var context = new PacketResponseContext(new CapturedEventSink());
            var first = Handshake(context, SteamA);
            LogAssert.Expect(LogType.Warning, new Regex("接続中"));
            var second = Handshake(context, "steam:2");

            // 付け替えを拒否し、切断時の解除対象も元のIDに保つ
            // Reject rebinding and preserve the original id used by disconnect cleanup
            Assert.AreEqual(HandshakeRejection.AlreadyConnected, second.Rejection);
            Assert.AreEqual(first.Accepted.PlayerId, context.PlayerId);
            Assert.IsFalse(_provider.GetRequiredService<PlayerIdentityRegistry>().GetSaveJsonObject().Entries.Exists(entry => entry.Identity == "steam:2"));
        }

        private InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack Handshake(PacketResponseContext context, string identity)
        {
            var payload = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(identity));
            return MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(
                _packet.GetPacketResponse(payload, context)[0]);
        }
    }
}
