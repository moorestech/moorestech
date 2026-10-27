using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Listen;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec.Listen
{
    // 入口の処理中に例外が出ても、HTTP 応答を閉じて送信側を待たせないことを固定する
    // Pin that an exception inside the entry still closes the HTTP response so the sender never hangs
    public sealed class RemoteExecHttpExchangeFaultTest
    {
        private HttpListener _listener;
        private TcpClient _sender;
        private RemoteExecTestFiles _files;
        private bool _wasEnabled;

        [SetUp]
        public void SetUp()
        {
            // 認証を通すため有効な起動とトークンを用意する
            // Prepare an enabled boot and a token so authentication passes
            _wasEnabled = RemoteExecLaunchOption.IsEnabled;
            _files = new RemoteExecTestFiles();
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
            RemoteExecAccessFile.Issue(0);

            // OSに空きポートを選ばせ、製品の待受帯とは別に試験用の待受を開く
            // Let the OS pick a free port and open a test listener outside the product band
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _sender = new TcpClient();
            _sender.Connect(IPAddress.Loopback, port);
        }

        [TearDown]
        public void TearDown()
        {
            _sender.Close();
            _listener.Close();
            RemoteExecAccessFile.Remove();
            RemoteExecLaunchOption.ResolveFromCommandLine(_wasEnabled ? new[] { "--remoteExec" } : Array.Empty<string>());
            _files.Restore();
        }

        [Test]
        public async Task 本文の読み取りで例外が出ても応答を閉じる()
        {
            // 壊れた chunked 本文は入口の本文読み取りで例外になる
            // A broken chunked body throws while the entry reads the request body
            var head = $"POST {RemoteExecEndpoint.Path} HTTP/1.1\r\nHost: 127.0.0.1\r\nTransfer-Encoding: chunked\r\n{RemoteExecAccessFile.HeaderName}: {RemoteExecAccessFile.Token}\r\n\r\nZZZZ\r\nbroken\r\n";
            var bytes = Encoding.ASCII.GetBytes(head);
            await _sender.GetStream().WriteAsync(bytes, 0, bytes.Length);
            var context = await _listener.GetContextAsync();

            LogAssert.Expect(LogType.Error, new Regex("要求の処理中に例外"));
            var handling = RemoteExecHttpExchange.HandleAsync(context, CancellationToken.None).AsTask();
            Assert.AreSame(handling, await Task.WhenAny(handling, Task.Delay(5000)), "応答処理が終わりませんでした");
            Assert.IsTrue(handling.IsFaulted, "例外が呼び出し側へ返りませんでした");

            var answered = WaitResponseOrCloseAsync();
            Assert.AreSame(answered, await Task.WhenAny(answered, Task.Delay(5000)), $"例外後も HTTP 応答が閉じられず送信側が待ち続けました: {handling.Exception?.GetBaseException().GetType()}");
        }

        // 送信側が応答の受信か切断を観測するまで待つ（keep-alive では応答後も接続が残る）
        // Wait until the sender observes a response or a disconnect (keep-alive leaves the connection open after a response)
        private async Task WaitResponseOrCloseAsync()
        {
            var buffer = new byte[1024];
            // ローカルOSソケット境界。切断（RST）も「閉じた」として扱う
            // Local OS socket boundary; a reset also counts as closed
            try
            {
                await _sender.GetStream().ReadAsync(buffer, 0, buffer.Length);
            }
            catch (IOException) { }
        }
    }
}
