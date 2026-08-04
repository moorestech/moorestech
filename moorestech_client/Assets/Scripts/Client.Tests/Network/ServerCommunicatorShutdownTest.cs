using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Client.Network;
using NUnit.Framework;

namespace Client.Tests.Network
{
    public class ServerCommunicatorShutdownTest
    {
        [Test]
        public void 受信待機中の明示的Closeは通信タスクを正常完了させる()
        {
            // 実Socket境界で受信を待機させ、別スレッドからの明示的Closeを再現する
            // Reproduce an explicit cross-thread Close while Receive blocks at a real socket boundary
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(IPAddress.Loopback, port);
                using var serverSocket = listener.AcceptSocket();
                var communicator = CreateCommunicator(clientSocket);

                // 受信スレッドがブロッキングReceiveへ入ってから明示的に接続を閉じる
                // Close explicitly after the receive thread has entered blocking Receive
                var communicationTask = Task.Run(() => communicator.StartCommunicat(null));
                Thread.Sleep(100);
                communicator.Close();

                Assert.That(communicationTask.Wait(TimeSpan.FromSeconds(2)), Is.True, "通信タスクが終了しませんでした");
                Assert.That(communicationTask.IsCompletedSuccessfully, Is.True);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Test]
        public void 明示的Close後のSendは例外なく送信しない()
        {
            // 実Socket接続を閉じた後の残存frame送信を再現する
            // Reproduce a send from a remaining frame after closing a real socket connection
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                using var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Connect(IPAddress.Loopback, port);
                using var serverSocket = listener.AcceptSocket();
                var communicator = CreateCommunicator(clientSocket);

                // Close後の送信は破棄し、peerへデータが届かないことも実境界で確認する
                // Discard sends after Close and verify at the real boundary that no data reaches the peer
                communicator.Close();
                communicator.Send(new byte[] { 7 });
                serverSocket.ReceiveTimeout = 2000;

                var receivedLength = serverSocket.Receive(new byte[1]);
                Assert.That(receivedLength, Is.Zero);
            }
            finally
            {
                listener.Stop();
            }
        }

        private static ServerCommunicator CreateCommunicator(Socket connectedSocket)
        {
            // 接続生成のPlayerLoop待ちを避け、実Socketをprivate ctorへ直接渡す
            // Avoid the connection factory's PlayerLoop wait and pass the real socket to the private constructor
            var constructor = typeof(ServerCommunicator).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Socket) },
                null);
            return (ServerCommunicator)constructor.Invoke(new object[] { connectedSocket });
        }
    }
}
