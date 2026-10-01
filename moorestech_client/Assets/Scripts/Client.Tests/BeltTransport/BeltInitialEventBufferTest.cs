using System;
using System.Collections;
using System.Text.RegularExpressions;
using Client.Game.InGame.BeltTransport;
using Client.Network.API;
using Client.Tests.Inventory;
using Core.BeltTransport;
using Cysharp.Threading.Tasks;
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Protocol.PacketResponse.Handshake;
using Server.Util.MessagePack;
using Server.Util.MessagePack.BeltTransport;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltInitialEventBufferTest
    {
        [UnityTest]
        public IEnumerator BufferedBeforeSubscriptionAndAfterSnapshotEventsApplyOnceTest() => UniTask.ToCoroutine(async () =>
        {
            var exchange = new PacketExchangeManager(null);
            var events = new VanillaApiEvent(exchange);
            Send(10); Send(11);
            await UniTask.Yield(); await UniTask.Yield();
            var handler = new BeltNetworkEventHandler(Initial(), events);
            Assert.AreEqual(10, handler.Replica.Tick);
            events.InitializeDispatch();
            Assert.AreEqual(11, handler.Replica.Tick);
            Send(12); Send(11);
            await UniTask.Yield(); await UniTask.Yield();
            Assert.AreEqual(12, handler.Replica.Tick);
            Assert.AreEqual(65, handler.Replica.Snapshot.Items[0].Progress);

            #region Internal
            void Send(ulong tick)
            {
                var payload = MessagePackSerializer.Serialize(new BeltTickMessagePack(BeltTestState.Tick(tick)));
                exchange.EnqueueReceivedPacket(MessagePackSerializer.Serialize(new EventStreamMessagePack(new EventMessagePack(BeltTickCompletedEventPacket.EventTag, payload))));
            }
            #endregion
        });
        [Test]
        public void MalformedPacketFailsStartupWaitVisiblyTest()
        {
            var events = new CapturingVanillaApiEvent();
            var handler = new BeltNetworkEventHandler(Initial(), events);
            LogAssert.Expect(LogType.Error, new Regex("Belt transport packet failed:"));
            events.Dispatch(BeltTickCompletedEventPacket.EventTag, new byte[] { 0xc1 });
            Assert.Throws<InvalidOperationException>(handler.ThrowIfFailed);
            Assert.AreEqual(10, handler.Replica.Tick);
        }
        internal static InitialHandshakeResponse Initial()
        {
            var handshake = new InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack(new HandshakeAcceptedMessagePack(new Vector3MessagePack(Vector3.zero), null, -1, null, null, null, 1));
            var snapshot = new BeltSnapshotMessagePack(new BeltCommittedSnapshot(10, BeltTestState.Snapshot(1, 32, true)));
            return new InitialHandshakeResponse(handshake, (default, default, default, default, default, default, default, default, snapshot));
        }
    }
}
