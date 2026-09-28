using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using System;
using System.IO;
using Client.RemoteExec.Access;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecRunnerTest
    {
        private RemoteExecTestFiles _files;
        private string _sessionName;
        private string _signalDirectory;

        [SetUp]
        public void SetUp()
        {
            _sessionName = "session_" + DateTime.UtcNow.Ticks;
            RemoteExecLedger.Initialize(_sessionName, null);
            _files = new RemoteExecTestFiles();
        }

        [TearDown]
        public void RestoreLedger()
        {
            _files.Restore();
            if (_signalDirectory != null && Directory.Exists(_signalDirectory)) Directory.Delete(_signalDirectory, true);
        }

        // Server.TestsはTearDownで自分の世代を閉じるため、実行順によらず既定停止状態を引き継ぐ
        // Server.Tests closes its generation in TearDown, preserving the stopped state regardless of order
        // 世代管理そのものの純粋テストは Server.Tests 側の ServerThreadActionQueueTest へ移した（Server.Boot が Client.Tests へ internal を開く必要をなくすため）
        // The pure generation-management tests moved to Server.Tests' ServerThreadActionQueueTest, so Server.Boot no longer needs to expose internals to Client.Tests

        [UnityTest]
        public IEnumerator コンパイルエラーは実行せず返る() => UniTask.ToCoroutine(async () =>
        {
            var result = await RemoteExecRunner.RunAsync("return 1 +;", RemoteExecTarget.Client);
            Assert.AreEqual(RemoteExecOutcome.CompileFailed, result.Outcome);
            Assert.IsNotEmpty(result.CompileErrors);
            var entries = _files.ReadLedgerEntries();
            Assert.AreEqual(2, entries.Length);
            Assert.AreEqual(entries[0].Value<long>("sequence"), entries[1].Value<long>("sequence"));
            Assert.AreEqual("CompileFailed", entries[1].Value<string>("outcome"));
        });

        [UnityTest]
        public IEnumerator 台帳より先に実行試行の印を残す() => UniTask.ToCoroutine(async () =>
        {
            _signalDirectory = Path.Combine(Path.GetTempPath(), "remote-exec-attempt-" + Guid.NewGuid().ToString("N"));
            var signalPath = Path.Combine(_signalDirectory, RemoteExecLedger.AttemptSignalFileName);
            RemoteExecLedger.Initialize(_sessionName, signalPath);
            var result = await RemoteExecRunner.RunAsync("return 1 +;", RemoteExecTarget.Client);
            Assert.AreEqual(RemoteExecOutcome.CompileFailed, result.Outcome);
            Assert.IsTrue(File.Exists(signalPath));
        });

        [UnityTest]
        public IEnumerator 結果文字列化の例外は成功として記録しない() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(UnityEngine.LogType.Warning, new Regex("送信コードの実行に失敗しました"));
            var result = await RemoteExecRunner.RunAsync(
                "return new Client.Tests.RemoteExec.RemoteExecRunnerTest.ThrowingResult();", RemoteExecTarget.Client);
            Assert.AreEqual(RemoteExecOutcome.RuntimeException, result.Outcome);
            Assert.IsNull(result.Result);
            StringAssert.Contains("result formatting failure", result.Exception);
            Assert.AreEqual("RuntimeException", _files.ReadLedgerEntries()[1].Value<string>("outcome"));
        });

        [UnityTest]
        public IEnumerator 開始前に取り消した要求は実行せず台帳に拒否を残す() => UniTask.ToCoroutine(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            LogAssert.Expect(UnityEngine.LogType.Warning, new Regex("切断済み要求の実行を取り消しました"));
            var result = await RemoteExecRunner.RunAsync("throw new System.Exception();", RemoteExecTarget.Client, cancellation.Token, null);
            Assert.AreEqual(RemoteExecOutcome.Rejected, result.Outcome);
            Assert.AreEqual("Rejected", _files.ReadLedgerEntries()[1].Value<string>("outcome"));
        });

        public sealed class ThrowingResult
        {
            public override string ToString()
            {
                throw new System.InvalidOperationException("result formatting failure");
            }
        }

        [UnityTest]
        public IEnumerator サーバー未起動は即時失敗する() => UniTask.ToCoroutine(async () =>
        {
            LogAssert.Expect(UnityEngine.LogType.Warning, "内蔵サーバーが起動していないため、サーバー側では実行できません");
            var result = await RemoteExecRunner.RunAsync("return 1;", RemoteExecTarget.Server);
            Assert.AreEqual(RemoteExecOutcome.Rejected, result.Outcome);
            Assert.That(result.Exception, Does.Contain("サーバー"));
        });
    }
}
