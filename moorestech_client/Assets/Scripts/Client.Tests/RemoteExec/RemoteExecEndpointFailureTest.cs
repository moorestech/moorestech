using System;
using System.IO;
using System.Reflection;
using Client.RemoteExec.Loading;
using Client.Game.Common;
using UniRx;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Client.RemoteExec;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using Client.RemoteExec.Listen;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public sealed class RemoteExecEndpointFailureTest
    {
        private RemoteExecTestFiles _files;
        private string _signalDirectory;

        [SetUp]
        public void SetUp()
        {
            // 印の置き場は製品と同じく必ず渡す。nullは「Initialize未了」で欠損ログが出る本番あり得ない状態
            // The signal directory is always supplied as in production; null means "not initialized", a state production never reaches
            _signalDirectory = Path.Combine(Path.GetTempPath(), "remote-exec-signal-" + Guid.NewGuid().ToString("N"));
            RemoteExecLedger.Initialize("session_" + DateTime.UtcNow.Ticks, Path.Combine(_signalDirectory, RemoteExecLedger.AttemptSignalFileName));
            _files = new RemoteExecTestFiles();
            RemoteExecAccessFile.Remove();
        }

        [TearDown]
        public void TearDown()
        {
            RemoteExecActivation.Deactivate();
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
            RemoteExecAccessFile.Remove();
            _files.Restore();
            if (Directory.Exists(_signalDirectory)) Directory.Delete(_signalDirectory, true);
        }

        [Test]
        public void 待受を開けなければHarmonyも接続情報も用意しない()
        {
            // 全探索ポートを塞ぎ起動失敗を作る
            // Block every searched port to force startup failure
            var blockers = new List<TcpListener>();
            for (var port = RemoteExecHttpListener.BasePort; port < RemoteExecHttpListener.BasePort + RemoteExecHttpListener.PortSearchRange; port++)
            {
                var blocker = new TcpListener(IPAddress.Loopback, port);
                // OSのネットワーク境界。他プロセスが既に使っているポートは塞がっているのでそのまま数える
                // OS network boundary; a port another process already holds is blocked anyway and counts as such
                try { blocker.Start(); blockers.Add(blocker); }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse) { Debug.LogWarning($"[Test] ポート {port} は既に使用中"); }
            }
            // アセンブリを別ロードし、ローダーのstatic状態を未初期化に隔離する
            // Load a separate assembly instance to isolate uninitialized loader statics
            var isolated = Assembly.Load(File.ReadAllBytes(typeof(RemoteExecActivation).Assembly.Location));
            var loader = isolated.GetType(typeof(RemoteExecHarmonyLoader).FullName, true);
            var registered = loader.GetField("_resolverRegistered", BindingFlags.Static | BindingFlags.NonPublic);
            var harmony = loader.GetField("_harmony", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(false, registered.GetValue(null));
            Assert.IsNull(harmony.GetValue(null));
            var activation = isolated.GetType(typeof(RemoteExecActivation).FullName, true);
            LogAssert.Expect(LogType.Warning, new Regex("--remoteExec が指定された"));
            isolated.GetType(typeof(RemoteExecLaunchOption).FullName, true)
                .GetMethod("ResolveFromCommandLine").Invoke(null, new object[] { new[] { "--remoteExec" } });
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("探索帯で待受を開けませんでした"));
                activation.GetMethod("ActivateIfRequested").Invoke(null, null);
                Assert.AreEqual(false, registered.GetValue(null), "起動拒否前にHarmony resolverを登録した");
                Assert.IsNull(harmony.GetValue(null));
                Assert.IsNull(isolated.GetType(typeof(RemoteExecAccessFile).FullName, true)
                    .GetProperty("Token", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null));
                Assert.IsFalse(File.Exists(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
            }
            finally
            {
                activation.GetMethod("Deactivate").Invoke(null, null);
                foreach (var blocker in blockers) blocker.Stop();
            }
        }

        [Test]
        public async Task 無効起動では要求を404で隠して理由をログに残す()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());

            LogAssert.Expect(LogType.Warning, new Regex("起動オプションが無いため要求を404で拒否"));
            var response = await RemoteExecEndpoint.HandleAsync(PostRequest(null, "{}"), CancellationToken.None);

            Assert.AreEqual(404, response.StatusCode);
            Assert.AreEqual(0, _files.ReadLedgerEntries().Length);
        }

        // 送信コードの実行時例外が、応答と台帳の両方へ同じoutcomeで残ることを本番経路のまま確かめる
        // Confirms a submitted code's runtime exception lands in both the response and the ledger with one outcome, through the production path alone
        [Test]
        public async Task 送信コードの実行時例外を失敗応答と台帳へ残す()
        {
            LogAssert.Expect(LogType.Warning, new Regex("--remoteExec が指定された"));
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
            LogAssert.Expect(LogType.Warning, new Regex("入口を開きました"));
            Assert.IsTrue(RemoteExecAccessFile.Stage(0));
            Assert.IsTrue(RemoteExecAccessFile.Publish());

            LogAssert.Expect(LogType.Warning, new Regex("送信コードの実行に失敗しました"));
            var result = await RemoteExecEndpoint.HandleAsync(PostRequest(RemoteExecAccessFile.Token, "{\"code\":\"throw new System.InvalidOperationException(\\\"execution failure\\\");\",\"target\":\"client\"}"), CancellationToken.None);

            var response = JObject.Parse(result.SerializeBody());
            Assert.AreEqual(200, result.StatusCode);
            Assert.AreEqual("RuntimeException", response.Value<string>("outcome"));
            StringAssert.Contains("execution failure", response.Value<string>("exception"));
            var entries = _files.ReadLedgerEntries();
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual("start", entries[0].Value<string>("event"));
            Assert.AreEqual("result", entries[1].Value<string>("event"));
            Assert.AreEqual("RuntimeException", entries[1].Value<string>("outcome"));
            Assert.AreEqual(entries[0].Value<long>("sequence"), entries[1].Value<long>("sequence"));
        }

        // 入口の拒否も実行結果と同じ型で返す。送信側は成功と拒否を1つの形で読める
        // An entry rejection comes back as the same type as a result, so the sender reads success and refusal through one shape
        [Test]
        public async Task 入口の拒否も実行結果と同じJSONで返す()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());

            LogAssert.Expect(LogType.Warning, new Regex("起動オプションが無いため要求を404で拒否"));
            var response = JObject.Parse((await RemoteExecEndpoint.HandleAsync(PostRequest(null, "{}"), CancellationToken.None)).SerializeBody());
            Assert.AreEqual("Unauthorized", response.Value<string>("outcome"));
            StringAssert.Contains("起動オプションが無い", response.Value<string>("rejectionReason"));
        }
        [Test]
        public async Task 取消コールバック例外でも撤去後の処理へ進む()
        {
            LogAssert.Expect(LogType.Warning, new Regex("--remoteExec が指定された"));
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
            var port = RemoteExecHttpListener.Start();
            Assert.IsNotNull(port);
            LogAssert.Expect(LogType.Warning, new Regex("入口を開きました"));
            Assert.IsTrue(RemoteExecAccessFile.Stage(port.Value));
            Assert.IsTrue(RemoteExecAccessFile.Publish());
            var lifetime = (CancellationTokenSource)typeof(RemoteExecHttpListener)
                .GetField("_lifetime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

            // 本番経路に取消時の例外を仕込む
            // Inject a throwing cancellation callback through the production path
            var body = new JObject
            {
                ["code"] = "cancellationToken.Register(() => throw new System.InvalidOperationException(\"cancel failure\")); return 1;",
                ["target"] = "client",
            }.ToString();
            var result = await RemoteExecEndpoint.HandleAsync(PostRequest(RemoteExecAccessFile.Token, body), lifetime.Token);
            Assert.AreEqual("Succeeded", JObject.Parse(result.SerializeBody()).Value<string>("outcome"));
            LogAssert.Expect(LogType.Error, new Regex(@"取消コールバック失敗[\s\S]*cancel failure"));
            GameShutdownEvent.ResetForNewSession();
            var participant = new ObservedShutdownParticipant();
            GameShutdownEvent.RegisterParticipant(participant);
            using var subscription = GameShutdownEvent.OnGameShutdown.Subscribe(_ => RemoteExecActivation.Deactivate());
            var shutdown = await GameShutdownEvent.FireGameShutdownAsync(GameShutdownReason.IntentionalExit);
            Assert.AreEqual(ShutdownFlushResult.Flushed, shutdown);
            Assert.IsTrue(participant.WasFlushed);
            GameShutdownEvent.ResetForNewSession();

            // 撤去後へ到達し、接続情報の消去を確認する（失敗理由は上のログ期待で確かめる）
            // Reach the continuation and verify withdrawn access details (the failure reason is checked by the expected log above)
            Assert.IsNull(RemoteExecAccessFile.Token);
            Assert.IsFalse(File.Exists(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
        }

        private sealed class ObservedShutdownParticipant : IGameShutdownParticipant
        {
            internal bool WasFlushed { get; private set; }
            public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
            {
                WasFlushed = true;
                return UniTask.FromResult(ShutdownFlushResult.Flushed);
            }
        }
        private static RemoteExecHttpRequest PostRequest(string token, string body)
        {
            return new RemoteExecHttpRequest(RemoteExecEndpoint.Path, "POST", false, token, new MemoryStream(Encoding.UTF8.GetBytes(body)));
        }
    }
}
