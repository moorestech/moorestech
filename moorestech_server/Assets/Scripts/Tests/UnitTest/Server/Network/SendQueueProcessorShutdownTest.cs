using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using Server.Boot.Loop.PacketProcessing;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Server.Network
{
    // 終了中に閉じた接続への送信失敗は想定内として記録し、終了意思の無い失敗だけをErrorに残す（受信側と同じ分類）
    // A send failure on a connection closed during shutdown is logged as expected; only failures without shutdown intent stay errors (same split as the receive side)
    public class SendQueueProcessorShutdownTest
    {
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void ShutdownSocketSend_IsExpectedOnlyAfterShutdownIntent(bool cancelServer, bool notifyShutdown)
        {
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            peer.Connect(listener.LocalEndPoint);
            using var accepted = listener.Accept();
            using var cancellation = new CancellationTokenSource();
            using var shutdownNotice = new CancellationTokenSource();
            var sender = new SendQueueProcessor(accepted, cancellation.Token, shutdownNotice.Token);
            var sendThread = (Thread)typeof(SendQueueProcessor).GetField("_sendThread", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sender);

            try
            {
                if (cancelServer) cancellation.Cancel();
                if (notifyShutdown) shutdownNotice.Cancel();
                if (cancelServer || notifyShutdown)
                {
                    LogAssert.Expect(LogType.Log, new Regex("^サーバー終了中のSocket送信を終了しました: .*"));
                }
                else
                {
                    // 終了意思の無い送信失敗は従来どおりErrorと例外を残す
                    // A send failure without shutdown intent still leaves the existing error and exception
                    LogAssert.Expect(LogType.Error, "送信スレッドでエラーが発生しました");
                    LogAssert.Expect(LogType.Exception, new Regex("^SocketException:.*", RegexOptions.Singleline));
                }

                // 送信側を閉じてから積むと、Sendが「ソケットは閉じられた」で決定的に失敗する（Play停止時の観測と同じ例外）
                // Enqueuing after closing the send side makes Send fail deterministically with "socket has been shut down" (the exception observed on play stop)
                accepted.Shutdown(SocketShutdown.Send);
                sender.EnqueueMessage(new byte[] { 1, 2, 3 });
                Assert.That(sendThread.Join(TimeSpan.FromSeconds(3)), Is.True, "送信失敗後も送信スレッドが残っています");
            }
            finally
            {
                sender.Dispose();
            }
        }
    }
}
