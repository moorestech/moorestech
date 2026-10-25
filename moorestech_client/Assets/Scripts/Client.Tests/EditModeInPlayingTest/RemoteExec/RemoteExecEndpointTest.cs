using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Listen;
using System.Net.Sockets;
using Client.Tests.RemoteExec;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest.RemoteExec
{
    [Category("CiShardClientPlay3")]
    public class RemoteExecEndpointTest
    {
        [UnityTest]
        public IEnumerator 起動認証入力と実行台帳を検証する()
        {
            EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;
            yield return RunScenarioAsync().ToCoroutine();
            yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        [UnityTearDown]
        public IEnumerator RestorePlayMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        private static async UniTask RunScenarioAsync()
        {
            var files = new RemoteExecTestFiles();
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false });
            try
            {
                // 無効な起動では待受も接続情報も作らない
                // A disabled boot opens no listener and writes no access details
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                RemoteExecActivation.ActivateIfRequested();
                Assert.IsFalse(File.Exists(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));

                // 本番経路でトークン/ポート取得
                // Get token/port through the production path
                RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
                RemoteExecActivation.ActivateIfRequested();
                var access = JObject.Parse(File.ReadAllText(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
                var token = access.Value<string>("token");
                var port = access.Value<int>("port");
                Assert.GreaterOrEqual(port, RemoteExecHttpListener.BasePort);
                Assert.Less(port, RemoteExecHttpListener.BasePort + RemoteExecHttpListener.PortSearchRange);
                Assert.AreEqual(64, token.Length);
                var url = $"http://127.0.0.1:{port}{RemoteExecEndpoint.Path}";
                var before = files.CountLedgerLines();

                // 拒否と理由ログを確認
                // Check rejections and reason logs
                LogAssert.Expect(LogType.Warning, new Regex("トークン不一致"));
                using (var missing = await SendAsync(client, url, HttpMethod.Post, "{}", null, null))
                    Assert.AreEqual(HttpStatusCode.Forbidden, missing.StatusCode);
                LogAssert.Expect(LogType.Warning, new Regex("トークン不一致"));
                using (var wrong = await SendAsync(client, url, HttpMethod.Post, "{}", "wrong", null))
                    Assert.AreEqual(HttpStatusCode.Forbidden, wrong.StatusCode);
                LogAssert.Expect(LogType.Warning, new Regex("Origin ヘッダ付き"));
                using (var browser = await SendAsync(client, url, HttpMethod.Post, "{}", token, "http://evil.example"))
                    Assert.AreEqual(HttpStatusCode.Forbidden, browser.StatusCode);
                LogAssert.Expect(LogType.Warning, new Regex("Origin ヘッダ付き"));
                using (var emptyOrigin = await SendAsync(client, url, HttpMethod.Post, "{}", token, ""))
                    Assert.AreEqual(HttpStatusCode.Forbidden, emptyOrigin.StatusCode);
                LogAssert.Expect(LogType.Warning, new Regex("POST 以外"));
                using (var method = await SendAsync(client, url, HttpMethod.Get, "{}", token, null))
                    Assert.AreEqual(HttpStatusCode.Forbidden, method.StatusCode);

                // 不正入力を理由付きで拒否
                // Reject invalid input with reasons
                foreach (var body in new[] { "{", "{\"code\":1,\"target\":\"client\"}", "{\"code\":\"return 2;\",\"target\":\"other\"}" })
                {
                    LogAssert.Expect(LogType.Warning, new Regex("要求を拒否しました"));
                    using var invalid = await SendAsync(client, url, HttpMethod.Post, body, token, null);
                    Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
                }
                Assert.AreEqual(before, files.CountLedgerLines());

                for (var i = 0; i < 2; i++)
                {
                    using var result = await SendAsync(client, url, HttpMethod.Post, "{\"code\":\"return 1 + 1;\",\"target\":\"client\"}", token, null);
                    Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
                    var json = JObject.Parse(await result.Content.ReadAsStringAsync());
                    Assert.AreEqual("Succeeded", json.Value<string>("outcome"));
                    Assert.AreEqual("2", json.Value<string>("result"));
                    Assert.IsNotNull(json["compileErrors"]);
                    Assert.IsNotNull(json["logs"]);
                    Assert.IsNull(json["ok"]);
                }
                Assert.AreEqual(before + 4, files.CountLedgerLines());

                // 失敗もHTTP成功+失敗台帳
                // Failure still returns HTTP success + failed ledger
                using var failed = await SendAsync(client, url, HttpMethod.Post, "{\"code\":\"return +;\",\"target\":\"client\"}", token, null);
                var failedJson = JObject.Parse(await failed.Content.ReadAsStringAsync());
                Assert.AreEqual(HttpStatusCode.OK, failed.StatusCode);
                Assert.AreEqual("CompileFailed", failedJson.Value<string>("outcome"));
                Assert.IsNotEmpty((JArray)failedJson["compileErrors"]);
                Assert.AreEqual(before + 6, files.CountLedgerLines());
                var entries = files.ReadLedgerEntries();
                for (var index = 0; index < entries.Length; index += 2)
                {
                    Assert.AreEqual("start", entries[index].Value<string>("event"));
                    Assert.AreEqual("result", entries[index + 1].Value<string>("event"));
                    Assert.AreEqual(entries[index].Value<long>("sequence"), entries[index + 1].Value<long>("sequence"));
                    Assert.AreEqual(index == 4 ? "return +;" : "return 1 + 1;", entries[index].Value<string>("code"));
                    Assert.AreEqual(index == 4 ? "CompileFailed" : "Succeeded", entries[index + 1].Value<string>("outcome"));
                }

                // 無効な起動へ切り替えると待受が閉じ、同じポートへ接続できない
                // Switching to a disabled boot closes the listener, so the same port refuses connections
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                var refused = false;
                using (var probe = new TcpClient())
                {
                    // 接続拒否はOSのネットワーク境界の例外として観測する
                    // A refused connection is observed as an exception at the OS network boundary
                    try { await probe.ConnectAsync(System.Net.IPAddress.Loopback, port); }
                    catch (SocketException) { refused = true; }
                }
                Assert.IsTrue(refused);
            }
            finally
            {
                // 失敗時も状態を復元
                // Restore state even on failure
                RemoteExecActivation.Deactivate();
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                files.Restore();
            }
        }

        private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, HttpMethod method, string body, string token, string origin)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            if (token != null) request.Headers.Add(RemoteExecAccessFile.HeaderName, token);
            if (origin != null) request.Headers.Add("Origin", origin);
            return await client.SendAsync(request);
        }
    }
}
