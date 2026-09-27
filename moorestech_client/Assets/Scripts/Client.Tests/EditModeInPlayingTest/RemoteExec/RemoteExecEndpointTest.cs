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
using Client.Tests.RemoteExec;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
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
            var kestrel = new KestrelServer();
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false });
            try
            {
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                await kestrel.StartAsync(new WebSocketHub());
                var url = $"http://127.0.0.1:{kestrel.ActualPort}{RemoteExecEndpoint.Path}";
                RemoteExecActivation.ActivateIfRequested(kestrel.ActualPort);
                Assert.IsFalse(File.Exists(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
                using (var disabled = await SendAsync(client, url, HttpMethod.Post, "{}", null, null))
                    Assert.AreEqual(HttpStatusCode.NotFound, disabled.StatusCode);

                // 製品と同じ有効化経路でトークンと実ポートを得る
                // Issue the token and actual port through the production activation path
                RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "-remote-exec" });
                RemoteExecActivation.ActivateIfRequested(kestrel.ActualPort);
                var access = JObject.Parse(File.ReadAllText(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
                var token = access.Value<string>("token");
                Assert.AreEqual(kestrel.ActualPort, access.Value<int>("port"));
                Assert.AreEqual(64, token.Length);
                var before = files.CountLedgerLines();

                // トークン・Origin・HTTPメソッドの拒否を理由ログとともに確認する
                // Check token, origin, and method rejections together with their reason logs
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

                // 不正なJSON・型・実行先は実行せず理由付きで拒否する
                // Reject malformed JSON, types, and targets with reasons before executing
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
                    Assert.IsTrue(json.Value<bool>("ok"));
                    Assert.AreEqual("2", json.Value<string>("result"));
                    Assert.IsNotNull(json["compileErrors"]);
                    Assert.IsNotNull(json["logs"]);
                    Assert.IsNull(json["Ok"]);
                }
                Assert.AreEqual(before + 4, files.CountLedgerLines());

                // コンパイル失敗もHTTP成功応答と失敗台帳を返す
                // A compilation failure still returns a structured response and a failed ledger entry
                using var failed = await SendAsync(client, url, HttpMethod.Post, "{\"code\":\"return +;\",\"target\":\"client\"}", token, null);
                var failedJson = JObject.Parse(await failed.Content.ReadAsStringAsync());
                Assert.AreEqual(HttpStatusCode.OK, failed.StatusCode);
                Assert.IsFalse(failedJson.Value<bool>("ok"));
                Assert.IsNotEmpty((JArray)failedJson["compileErrors"]);
                Assert.AreEqual(before + 6, files.CountLedgerLines());
                var entries = files.ReadLedgerEntries();
                for (var index = 0; index < entries.Length; index += 2)
                {
                    Assert.AreEqual("start", entries[index].Value<string>("event"));
                    Assert.AreEqual("result", entries[index + 1].Value<string>("event"));
                    Assert.AreEqual(entries[index].Value<long>("sequence"), entries[index + 1].Value<long>("sequence"));
                    Assert.AreEqual(index == 4 ? "return +;" : "return 1 + 1;", entries[index].Value<string>("code"));
                    Assert.AreEqual(index != 4, entries[index + 1].Value<bool>("ok"));
                }
            }
            finally
            {
                // 失敗時も待受と実ユーザーファイルを元へ戻す
                // Restore the listener and real user files even if an assertion fails
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                try { await kestrel.StopAsync(); }
                finally { files.Restore(); }
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
