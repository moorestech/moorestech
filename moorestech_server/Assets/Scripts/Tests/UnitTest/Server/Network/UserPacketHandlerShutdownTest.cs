using System;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Server.Network
{
    public class UserPacketHandlerShutdownTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public void RealPeerReset_IsExpectedOnlyAfterServerCancellation(bool cancelServer)
        {
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            using var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            peer.Connect(listener.LocalEndPoint);
            using var accepted = listener.Accept();
            using var cancellation = new CancellationTokenSource();
            using var entered = new ManualResetEventSlim();
            var sender = new SendQueueProcessor(accepted);
            var queue = new TickEndPacketQueue();
            var context = new PacketResponseContext(sender);
            var receiver = new ReceiveQueueProcessor(null, sender, context, queue, null);
            var handler = new UserPacketHandler(accepted, receiver, sender, null, null, context, null, queue);
            var thread = new Thread(() =>
            {
                entered.Set();
                handler.StartListen(cancellation.Token);
            });

            // 実Receiveで待機した状態からcancel→RSTを送り、ループ先頭のcancel検査だけでは覆えない境界を通す
            // Send cancellation then RST from a blocked real Receive, beyond the loop's cancellation check
            thread.Start();
            try
            {
                Assert.That(entered.Wait(TimeSpan.FromSeconds(2)), Is.True);
                Thread.Sleep(100);
                Debug.Log($"[UserPacketHandlerShutdown] blocked={thread.ThreadState} cancel={cancelServer} utc={DateTime.UtcNow:O}");
                if (cancelServer)
                {
                    cancellation.Cancel();
                    LogAssert.Expect(LogType.Log, "サーバー終了中のSocket受信を終了しました: ConnectionReset");
                }
                else
                {
                    // 未cancelの本物のRSTは既存のErrorと例外を厳密に要求する
                    // An uncancelled real RST must still produce the existing Error and exception
                    LogAssert.Expect(LogType.Error, "moorestech内のロジックによるエラーで切断");
                    LogAssert.Expect(LogType.Exception, new Regex("^SocketException:.*", RegexOptions.Singleline));
                }
                peer.LingerState = new LingerOption(true, 0);
                peer.Close();
                Assert.That(thread.Join(TimeSpan.FromSeconds(3)), Is.True, "RST後も受信スレッドが残っています");
            }
            finally
            {
                // assert失敗時にも自分の受信・送信スレッドを残さない
                // Leave neither owned receive nor send threads behind on assertion failures
                cancellation.Cancel();
                accepted.Close();
                Assert.That(thread.Join(TimeSpan.FromSeconds(3)), Is.True);
            }
        }
    }
}
