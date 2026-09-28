using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.PlayerConnection;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest.Handshake
{
    public class InitialHandshakeClosedConnectionTest
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void FailedBindPreservesIdentityAndInventoryTest(bool hasCandidate, bool closeDuringRegistration)
        {
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var registry = provider.GetRequiredService<PlayerIdentityRegistry>();
            if (hasCandidate)
            {
                registry.Load(new PlayersSaveJsonObject(3, 2, new List<PlayerIdentityEntryJsonObject>
                {
                    new(1, null),
                    new(2, null),
                }));
            }
            var before = JsonConvert.SerializeObject(registry.GetSaveJsonObject());
            var inventory = provider.GetRequiredService<IPlayerInventoryDataStore>();
            var inventoryBefore = JsonConvert.SerializeObject(inventory.GetSaveJsonObject());
            var sink = new CapturedEventSink();
            var context = new PacketResponseContext(sink);

            // 登録の途中で切断する経路も作り、バインドとの競合を決定論的に再現する
            // Close during registration too, deterministically reproducing the bind race
            var events = provider.GetRequiredService<EventProtocolProvider>();
            using var subscription = events.OnPlayerEventStreamRegistered.Subscribe(_ =>
            {
                if (closeDuringRegistration) context.MarkClosedAndGetPlayerId();
            });
            if (!closeDuringRegistration) context.MarkClosedAndGetPlayerId();
            var payload = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack("steam:1"));
            LogAssert.Expect(LogType.Warning, new Regex("切断"));
            var response = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(
                packet.GetPacketResponse(payload, context)[0]);

            // 候補・次ID・持ち物を含めて一切確定されていないことを確かめる
            // Verify nothing was committed, including the candidate, next id and inventory
            Assert.AreEqual(HandshakeRejection.ConnectionClosed, response.Rejection);
            Assert.IsNull(response.Accepted);
            Assert.IsNull(context.PlayerId);
            Assert.AreEqual(before, JsonConvert.SerializeObject(registry.GetSaveJsonObject()));
            Assert.AreEqual(inventoryBefore, JsonConvert.SerializeObject(inventory.GetSaveJsonObject()));
            Assert.IsFalse(provider.GetRequiredService<IPlayerConnectionChecker>().IsConnected(hasCandidate ? 2 : 1));
            sink.TakeAll();
            events.AddBroadcastEvent("test:probe", new byte[0]);
            Assert.IsEmpty(sink.Events);
        }
    }
}
