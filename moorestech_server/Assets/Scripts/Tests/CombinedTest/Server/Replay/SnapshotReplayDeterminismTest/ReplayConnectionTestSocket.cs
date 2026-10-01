using System;
using System.Net;
using System.Net.Sockets;
using Game.SaveLoad.Snapshot;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;

namespace Tests.CombinedTest.Server.Replay.SnapshotReplayDeterminismTest
{
    // 本番の受信キューと送信キューを使う再生テスト用のループバック接続
    // Loopback connection for replay tests using production receive and send queues
    internal sealed class ReplayConnectionTestSocket : IDisposable
    {
        private readonly Socket _listener;
        private readonly Socket _client;
        private readonly Socket _accepted;
        private readonly SendQueueProcessor _sender;

        internal ReplayConnectionTestSocket()
        {
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _listener.Listen(1);
            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _client.Connect(_listener.LocalEndPoint);
            _accepted = _listener.Accept();
            _sender = new SendQueueProcessor(_accepted);
        }

        internal ReceiveQueueProcessor CreateReceiver(PacketResponseCreator creator, PacketResponseContext context,
            TickEndPacketQueue queue, ReceivedPacketLog log)
        {
            return new ReceiveQueueProcessor(creator, _sender, context, queue, log);
        }

        public void Dispose()
        {
            _sender.Dispose();
            _accepted.Dispose();
            _client.Dispose();
            _listener.Dispose();
        }
    }
}
