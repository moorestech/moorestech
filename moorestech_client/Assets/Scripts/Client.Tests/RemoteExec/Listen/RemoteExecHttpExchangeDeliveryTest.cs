using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Client.RemoteExec.Listen;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec.Listen
{
    // 応答の未達を、待受停止による中断と送信側の切断とで分けて扱うことを固定する
    // Pin that undelivered responses are told apart: listener-stop interruption versus sender disconnect
    public sealed class RemoteExecHttpExchangeDeliveryTest
    {
        private HttpListener _listener;
        private TcpClient _sender;

        [SetUp]
        public void SetUp()
        {
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
        }

        [Test]
        public async Task 送信側が切断した応答の未達は失敗として返る()
        {
            var context = await ReceiveAsync();

            // 応答を書く前に送信側をRSTで切る
            // Reset the sender before the response is written
            _sender.LingerState = new LingerOption(true, 0);
            _sender.Close();
            await Task.Delay(200);

            LogAssert.Expect(LogType.Warning, new Regex("応答を届けられませんでした"));
            var handling = RemoteExecHttpExchange.HandleAsync(context, CancellationToken.None).AsTask();
            Assert.AreSame(handling, await Task.WhenAny(handling, Task.Delay(5000)), "応答処理が終わりませんでした");
            Assert.IsTrue(handling.IsFaulted, "切断で届かなかった応答が成功として完了しました");
        }

        [Test]
        public async Task 待受停止による中断は失敗として返らない()
        {
            var context = await ReceiveAsync();
            using var stopped = new CancellationTokenSource();
            stopped.Cancel();

            LogAssert.Expect(LogType.Warning, new Regex("待受の停止により応答を中断しました"));
            var handling = RemoteExecHttpExchange.HandleAsync(context, stopped.Token).AsTask();
            Assert.AreSame(handling, await Task.WhenAny(handling, Task.Delay(5000)), "応答処理が終わりませんでした");
            Assert.IsFalse(handling.IsFaulted, "待受停止による中断が失敗として返りました");
        }

        private async Task<HttpListenerContext> ReceiveAsync()
        {
            // 遠隔実行以外の経路へ送り、入口が404を即答する要求を作る
            // Send to a non-remote-exec route so the entry answers 404 immediately
            var request = Encoding.ASCII.GetBytes("GET /not-remote-exec HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");
            await _sender.GetStream().WriteAsync(request, 0, request.Length);
            return await _listener.GetContextAsync();
        }
    }
}
