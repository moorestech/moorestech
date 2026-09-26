using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Network;
using Client.Network.API;
using Client.Tests.Common;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.TickSynchronization
{
    public class TrainResyncRequestRaceTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void SnapshotBeforeAck_ReleasesItsGateAndOldFailureCannotReleaseNextRequest(bool startNextRequest)
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            using (services)
            using (var client = new TrainSnapshotClientFixture())
            using (var transport = new ResyncTransport())
            {
                var sink = new CapturedEventSink();
                services.GetRequiredService<Server.Event.EventProtocolProvider>().RegisterPlayer(1, sink);
                var rail = sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag).Payload;
                var train = sink.Events.Single(e => e.Tag == TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag).Payload;
                Request(1);
                Assert.AreEqual(1, transport.Waiters.Count);
                client.ApplyRail(rail);
                client.ApplyTrain(train);

                // snapshot完了はackを待たずに現要求を解放する。
                // Snapshot completion releases the current request without waiting for its ack.
                if (!startNextRequest)
                {
                    client.Context.Hashes.EnqueueHash(TrainUnitHashBuffer.DummyHash, TrainUnitHashBuffer.DummyHash, 0, 2);
                    Assert.IsTrue(client.Gate.CanAdvanceTick(2));
                    transport.Complete(1, PacketWaitCompletionReason.Received);
                    return;
                }

                Request(2);
                Assert.AreEqual(2, transport.Waiters.Count);
                transport.Complete(1, PacketWaitCompletionReason.Timeout);
                client.Context.Hashes.EnqueueHash(TrainUnitHashBuffer.DummyHash, TrainUnitHashBuffer.DummyHash, 0, 3);
                Assert.IsFalse(client.Gate.CanAdvanceTick(3), "Old ack failure released the newer request");
                transport.Complete(2, PacketWaitCompletionReason.Received);
                Assert.IsFalse(client.Gate.CanAdvanceTick(3), "Ack alone released the newer request");
                client.ApplyRail(rail);
                client.ApplyTrain(train);
                Assert.IsTrue(client.Gate.CanAdvanceTick(3));

                #region Internal
                void Request(uint sequence)
                {
                    client.Context.Hashes.EnqueueHash(client.Trains.ComputeCurrentHash() ^ 1u, client.Rails.ComputeCurrentHash(), 0, sequence);
                    LogAssert.Expect(LogType.Warning, new Regex("^\\[TrainUnitHashVerifier\\] Hash mismatch detected"));
                    Assert.IsFalse(client.Gate.CanAdvanceTick(sequence));
                }
                #endregion
            }
        }

        // 既存hash gateテストと同じ実socket境界で、ack到着順だけを制御する。
        // Control only ack ordering at the same real socket boundary as the existing hash-gate test.
        private sealed class ResyncTransport : IDisposable
        {
            private readonly Socket _socket;
            private readonly Socket _peer;
            private readonly ServerCommunicator _communicator;
            private readonly VanillaApi _previous;
            public readonly Dictionary<int, ResponseWaiter> Waiters = new();

            public ResyncTransport()
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.Connect((IPEndPoint)listener.LocalEndpoint);
                _peer = listener.AcceptSocket();
                listener.Stop();
                var constructor = typeof(ServerCommunicator).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(Socket) }, null);
                _communicator = (ServerCommunicator)constructor.Invoke(new object[] { _socket });
                var exchange = (PacketExchangeManager)FormatterServices.GetUninitializedObject(typeof(PacketExchangeManager));
                TestReflection.SetField(exchange, "_packetSender", new PacketSender(_communicator));
                TestReflection.SetField(exchange, "_responseWaiters", Waiters);
                var response = (VanillaApiWithResponse)FormatterServices.GetUninitializedObject(typeof(VanillaApiWithResponse));
                TestReflection.SetField(response, "_packetExchangeManager", exchange);
                var api = (VanillaApi)FormatterServices.GetUninitializedObject(typeof(VanillaApi));
                typeof(VanillaApi).GetField("Response").SetValue(api, response);
                _previous = ClientContext.VanillaApi;
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", api);
            }

            public void Complete(int sequence, PacketWaitCompletionReason reason)
            {
                var payload = MessagePackSerializer.Serialize(new TrainResyncProtocol.ResponseMessagePack(true));
                Waiters[sequence].WaitSubject.OnNext((payload, reason));
            }

            public void Dispose()
            {
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", _previous);
                _communicator.Close();
                _peer.Dispose();
                _socket.Dispose();
            }
        }
    }
}
