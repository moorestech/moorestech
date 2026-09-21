using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Serialization;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Client.Tests.Common;
using NUnit.Framework;
using Server.Event;
using Server.Protocol.PacketResponse;
using UniRx;

namespace Client.Tests.PlaceSystem.TrainCostIntegration
{
    internal sealed class PlacementPacketCapture : IDisposable
    {
        internal readonly VanillaApi Api;
        private readonly Socket _peer;
        private readonly ServerCommunicator _communicator;
        private readonly Subject<EventMessagePack> _events = new();

        internal PlacementPacketCapture()
        {
            // 既存通信テストと同じ実Socket境界で送信を観測する
            // Observe outgoing requests at the real socket boundary used by existing network tests
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect((IPEndPoint)listener.LocalEndpoint);
            _peer = listener.AcceptSocket();
            listener.Stop();
            var constructor = typeof(ServerCommunicator).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Socket) }, null);
            _communicator = (ServerCommunicator)constructor.Invoke(new object[] { socket });
            var sender = new PacketSender(_communicator);

            // EditModeでは常駐受信ループを起動せず、実要求送信に必要な状態だけ用意する
            // Avoid perpetual receive loops in EditMode while retaining the real request-sending path
            var exchange = (PacketExchangeManager)FormatterServices.GetUninitializedObject(typeof(PacketExchangeManager));
            TestReflection.SetField(exchange, "_packetSender", sender);
            TestReflection.SetField(exchange, "_responseWaiters", new Dictionary<int, ResponseWaiter>());
            TestReflection.SetField(exchange, "_eventPacketSubject", _events);
            Api = new VanillaApi(exchange, sender, _communicator, new PlayerConnectionSetting(1));

            // 空要求を送って観測系自体が働くことを先に固定する
            // Prove the capture works before asserting that placement emits no request
            Api.SendOnly.PlaceBlock(new List<PlaceInfo>());
            Assert.IsTrue(_peer.Poll(1000000, SelectMode.SelectRead));
            var probe = new byte[_peer.Available];
            Assert.Greater(_peer.Receive(probe), 0);
        }

        internal void AssertNoRequest()
        {
            Assert.IsFalse(_peer.Poll(1000, SelectMode.SelectRead), "素材不足の配置要求がSocketへ送られた");
        }

        public void Dispose()
        {
            _events.Dispose();
            _communicator.Close();
            _peer.Dispose();
        }
    }
}
