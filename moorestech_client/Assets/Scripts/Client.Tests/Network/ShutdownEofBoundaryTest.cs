using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Text.RegularExpressions;
using Client.Game.Common;
using Client.Network;
using Client.Starter.Initialization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Network
{
    public class ShutdownEofBoundaryTest
    {
        [TestCase(GameShutdownReason.IntentionalExit)]
        [TestCase(GameShutdownReason.UnawaitableExit)]
        public void ShutdownIntent_AllowsPeerEofBeforeCloseAndPreservesFlushTraffic(GameShutdownReason reason)
        {
            GameShutdownEvent.ResetForNewSession();
            using var connection = new Connection();
            ServerConnectionInitializer.RegisterConnectionShutdown(connection.Communicator, CancellationToken.None);
            var participant = new PeerClosingFlushParticipant(connection);
            GameShutdownEvent.RegisterParticipant(participant);

            // 本番の通知→参加者→実EOFを通し、flush完了前にはCloseしない
            // Exercise production intent, participant, and real EOF without Close before flush completes
            Debug.Log($"[ShutdownEofBoundary] fire reason={reason} utc={DateTime.UtcNow:O}");
            var shutdown = GameShutdownEvent.FireGameShutdownAsync(reason);
            Assert.That(shutdown.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(participant.ReceivedPayload, Is.EqualTo(7));
            participant.Complete();
            Assert.That(shutdown.GetAwaiter().GetResult(), Is.EqualTo(ShutdownFlushResult.Flushed));
        }

        [Test]
        public void ExitCancellationBeforeShutdownNotice_AllowsPeerEof()
        {
            GameShutdownEvent.ResetForNewSession();
            using var connection = new Connection();
            using var exit = new CancellationTokenSource();
            ServerConnectionInitializer.RegisterConnectionShutdown(connection.Communicator, exit.Token);

            // Play停止のtokenだけ先に発火し、quitting通知より早いpeer終了を通す
            // Fire only the play-stop token and exercise peer closure before the quitting notice
            exit.Cancel();
            connection.Peer.Shutdown(SocketShutdown.Send);
            Assert.That(connection.Communicator.StartCommunicat(null).IsCompletedSuccessfully, Is.True);
            GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.UnawaitableExit).GetAwaiter().GetResult();
        }

        [Test]
        public void UnexpectedPeerEof_RemainsAnError()
        {
            using var connection = new Connection();
            connection.Peer.Shutdown(SocketShutdown.Send);

            // 通常運転中の本物のEOFだけを厳密に期待し、終了時の抑制と区別する
            // Strictly expect a real EOF during normal operation, distinct from shutdown normalization
            LogAssert.Expect(LogType.Error, "ストリームがゼロによる切断");
            Assert.That(connection.Communicator.StartCommunicat(null).IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public void UnexpectedSocketReset_RemainsAnErrorAndThrows()
        {
            using var connection = new Connection();
            connection.Peer.LingerState = new LingerOption(true, 0);
            connection.Peer.Close();

            // 本物のSocket障害はログだけでなく例外も呼び出し元へ返す
            // A real socket fault must still log and propagate its exception to the caller
            LogAssert.Expect(LogType.Error, "エラーによりサーバーから切断されました");
            LogAssert.Expect(LogType.Error, new Regex("^Message "));
            LogAssert.Expect(LogType.Error, new Regex("^受信パケット内容 JSON:"));
            var exception = Assert.Throws<SocketException>(() => connection.Communicator.StartCommunicat(null));
            Assert.That(exception.SocketErrorCode, Is.EqualTo(SocketError.ConnectionReset));
        }

        private sealed class PeerClosingFlushParticipant : IGameShutdownParticipant
        {
            private readonly Connection _connection;
            private readonly UniTaskCompletionSource<ShutdownFlushResult> _completion = new();
            internal byte ReceivedPayload { get; private set; }

            internal PeerClosingFlushParticipant(Connection connection)
            {
                _connection = connection;
            }

            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                // 終了意思表明後にも最終要求が相手へ到達することを実Socketで確認する
                // Verify at a real socket that a final request still reaches the peer after shutdown intent
                _connection.Communicator.Send(new byte[] { 7 });
                var frame = new byte[5];
                var offset = 0;
                while (offset < frame.Length)
                {
                    var received = _connection.Peer.Receive(frame, offset, frame.Length - offset, SocketFlags.None);
                    Assert.That(received, Is.GreaterThan(0));
                    offset += received;
                }
                ReceivedPayload = frame[4];
                Debug.Log($"[ShutdownEofBoundary] flush send arrived; peer FIN utc={DateTime.UtcNow:O}");
                _connection.Peer.Shutdown(SocketShutdown.Send);
                Assert.That(_connection.Communicator.StartCommunicat(null).IsCompletedSuccessfully, Is.True);
                return _completion.Task;
            }

            internal void Complete()
            {
                _completion.TrySetResult(ShutdownFlushResult.Flushed);
            }
        }

        private sealed class Connection : IDisposable
        {
            private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
            private readonly Socket _client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            internal readonly Socket Peer;
            internal readonly ServerCommunicator Communicator;

            internal Connection()
            {
                _listener.Start();
                _client.Connect(_listener.LocalEndpoint);
                Peer = _listener.AcceptSocket();
                Peer.ReceiveTimeout = 2000;
                _client.ReceiveTimeout = 2000;
                var constructor = typeof(ServerCommunicator).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(Socket) }, null);
                Communicator = (ServerCommunicator)constructor.Invoke(new object[] { _client });
            }

            public void Dispose()
            {
                Communicator.Close();
                Peer.Dispose();
                _client.Dispose();
                _listener.Stop();
            }
        }
    }
}
