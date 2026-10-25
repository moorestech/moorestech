using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.RemoteExec;
using Client.RemoteExec.Listen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public sealed class RemoteExecHttpListenerTest
    {
        [TearDown]
        public void TearDown()
        {
            RemoteExecHttpListener.Stop();
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
        }

        [Test]
        public void 探索帯がWebUiのKestrel帯とVite帯に重ならない()
        {
            // ポート帯の台帳は2アセンブリに割れるので、重ならないことを機械で固定する
            // The port bands live in two assemblies, so their disjointness is pinned mechanically
            var start = RemoteExecHttpListener.BasePort;
            var end = start + RemoteExecHttpListener.PortSearchRange;
            foreach (var otherStart in new[] { Client.WebUiHost.Common.WebUiPortConfig.KestrelBasePort, Client.WebUiHost.Common.WebUiPortConfig.ViteBasePort })
            {
                var otherEnd = otherStart + Client.WebUiHost.Common.WebUiPortConfig.PortSearchRange;
                Assert.IsTrue(end <= otherStart || otherEnd <= start, $"遠隔実行の帯 {start}..{end - 1} が {otherStart}..{otherEnd - 1} と重なる");
            }
        }

        [Test]
        public void 基準ポートが使用中なら次のポートで開く()
        {
            var blocker = new TcpListener(IPAddress.Loopback, RemoteExecHttpListener.BasePort);
            // OSのネットワーク境界。他プロセスが既に基準ポートを使っていても「使用中」の前提は同じ
            // OS network boundary; if another process already holds the base port the "occupied" premise still holds
            try { blocker.Start(); }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse) { Debug.LogWarning("[Test] 基準ポートは既に使用中"); }
            var port = RemoteExecHttpListener.Start();
            blocker.Stop();

            Assert.IsNotNull(port);
            Assert.Greater(port.Value, RemoteExecHttpListener.BasePort);
            Assert.Less(port.Value, RemoteExecHttpListener.BasePort + RemoteExecHttpListener.PortSearchRange);
        }

        [Test]
        public async Task 停止後は同じポートへ接続できない()
        {
            var port = RemoteExecHttpListener.Start();
            Assert.IsNotNull(port);
            RemoteExecHttpListener.Stop();

            // 接続拒否はOSのネットワーク境界の例外として観測する
            // A refused connection is observed as an exception at the OS network boundary
            var refused = false;
            using var client = new TcpClient();
            try { await client.ConnectAsync(IPAddress.Loopback, port.Value); }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.ConnectionRefused) { refused = true; }
            Assert.IsTrue(refused);
        }

        [Test]
        public async Task 遠隔実行以外の経路は404で理由をログに残す()
        {
            var port = RemoteExecHttpListener.Start();
            Assert.IsNotNull(port);
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false });

            LogAssert.Expect(LogType.Warning, new Regex("未知の経路です: /api/ping"));
            using var response = await client.PostAsync($"http://127.0.0.1:{port.Value}/api/ping", new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            StringAssert.Contains("\"outcome\":\"BadRequest\"", await response.Content.ReadAsStringAsync());
        }
    }
}
