using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.TickSynchronization;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Network;
using Client.Network.API;
using Client.Tests.Common;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.TickSynchronization
{
    public class TrainTickHashGateTest
    {
        private TrainTickContext _context;
        private RailGraphClientCache _rails;
        private TrainUnitClientCache _trains;
        private TrainFullSnapshotEventNetworkHandler _handler;
        private TrainUnitHashVerifier _gate;

        [SetUp]
        public void SetUp()
        {
            _context = new TrainTickContext();
            _rails = (RailGraphClientCache)Activator.CreateInstance(typeof(RailGraphClientCache), true);
            _trains = new TrainUnitClientCache(_rails);
            _handler = new TrainFullSnapshotEventNetworkHandler(null, null, _context);
            _gate = new TrainUnitHashVerifier(_handler, _context, _trains, _rails);
        }

        [TearDown]
        public void TearDown()
        {
            _gate.Dispose();
            _handler.Dispose();
        }

        [Test]
        public void DummyHash_AllowsAdvanceAndRecordsAppliedId()
        {
            _context.Hashes.EnqueueHash(TrainUnitHashBuffer.DummyHash, TrainUnitHashBuffer.DummyHash, 0, 1);
            Assert.IsTrue(_gate.CanAdvanceTick(1));
            Assert.AreEqual(1ul, _context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void MatchingHash_AllowsAdvanceAndRecordsAppliedId()
        {
            _context.Hashes.EnqueueHash(_trains.ComputeCurrentHash(), _rails.ComputeCurrentHash(), 0, 1);
            Assert.IsTrue(_gate.CanAdvanceTick(1));
            Assert.AreEqual(1ul, _context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void FutureHashOnly_ForceSlipsWithoutRecordingAppliedId()
        {
            _context.Hashes.EnqueueHash(0, 0, 1, 1);
            LogAssert.Expect(LogType.Warning, "tick force slip! expected=0_1, firstBuffered=1_1");
            Assert.IsTrue(_gate.CanAdvanceTick(1));
            Assert.AreEqual(0ul, _context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void NoHash_WaitsWithoutWarning()
        {
            Assert.IsFalse(_gate.CanAdvanceTick(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ResyncPending_BlocksEvenDummyHash()
        {
            TestReflection.SetField(_gate, "_resyncInProgress", 1);
            _context.Hashes.EnqueueHash(TrainUnitHashBuffer.DummyHash, TrainUnitHashBuffer.DummyHash, 0, 1);
            Assert.IsFalse(_gate.CanAdvanceTick(1));
            Assert.AreEqual(0ul, _context.State.GetAppliedTickUnifiedId());
        }

        [Test]
        public void HashReads_DoNotConsumeAndDiscardIsStrictlyOlder()
        {
            _context.Hashes.EnqueueHash(12, 34, 0, 1);
            Assert.IsTrue(_context.Hashes.TryDequeueHashAtTickSequenceId(1, out var first));
            Assert.IsTrue(_context.Hashes.TryDequeueHashAtTickSequenceId(1, out var second));
            Assert.AreEqual(first, second);
            _context.Hashes.DiscardHashesOlderThan(1);
            Assert.IsTrue(_context.Hashes.TryGetFirstHashTickUnifiedId(out var id));
            Assert.AreEqual(1ul, id);
            _context.Hashes.DiscardHashesOlderThan(2);
            Assert.IsFalse(_context.Hashes.TryGetFirstHashTickUnifiedId(out _));
        }

        [Test]
        public void MismatchingHash_RequestsResyncAndAckDoesNotReleaseGate()
        {
            // 実Socketへの要求送信を観測し、常駐受信ループは起動しない。
            // Observe the real socket request without starting perpetual receive loops.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect((IPEndPoint)listener.LocalEndpoint);
            using var peer = listener.AcceptSocket();
            listener.Stop();
            var constructor = typeof(ServerCommunicator).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Socket) }, null);
            var communicator = (ServerCommunicator)constructor.Invoke(new object[] { socket });
            var waiters = new Dictionary<int, ResponseWaiter>();
            var exchange = (PacketExchangeManager)FormatterServices.GetUninitializedObject(typeof(PacketExchangeManager));
            TestReflection.SetField(exchange, "_packetSender", new PacketSender(communicator));
            TestReflection.SetField(exchange, "_responseWaiters", waiters);
            var response = (VanillaApiWithResponse)FormatterServices.GetUninitializedObject(typeof(VanillaApiWithResponse));
            TestReflection.SetField(response, "_packetExchangeManager", exchange);
            var api = (VanillaApi)FormatterServices.GetUninitializedObject(typeof(VanillaApi));
            typeof(VanillaApi).GetField("Response").SetValue(api, response);
            var previous = ClientContext.VanillaApi;
            TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", api);
            // 外部Socket検証の失敗時にも共有contextと接続を必ず復元する。
            // Restore the shared context and connection even if socket assertions fail.
            try
            {
                _context.Hashes.EnqueueHash(_trains.ComputeCurrentHash() ^ 1u, _rails.ComputeCurrentHash(), 0, 1);
                LogAssert.Expect(LogType.Warning, new Regex("^\\[TrainUnitHashVerifier\\] Hash mismatch detected"));
                Assert.IsFalse(_gate.CanAdvanceTick(1));
                Assert.AreEqual(0ul, _context.State.GetAppliedTickUnifiedId());
                Assert.IsTrue(peer.Poll(1000000, SelectMode.SelectRead));
                Assert.AreEqual(1, waiters.Count);

                // ackが届いてもsnapshot適用までは次tickへ進めない。
                // Even an acknowledged request cannot advance before snapshot application.
                var ack = MessagePackSerializer.Serialize(new TrainResyncProtocol.ResponseMessagePack(true));
                waiters[1].WaitSubject.OnNext((ack, PacketWaitCompletionReason.Received));
                _context.Hashes.EnqueueHash(TrainUnitHashBuffer.DummyHash, TrainUnitHashBuffer.DummyHash, 0, 2);
                Assert.IsFalse(_gate.CanAdvanceTick(2));
            }
            finally
            {
                TestReflection.SetStaticProperty(typeof(ClientContext), "VanillaApi", previous);
                communicator.Close();
            }
        }
    }
}
