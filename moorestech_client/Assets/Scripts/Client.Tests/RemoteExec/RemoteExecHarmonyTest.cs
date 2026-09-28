using System.Collections;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecHarmonyTest
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
        }

        [TearDown]
        public void TearDown()
        {
            _files.Restore();
            if (Directory.Exists(_signalDirectory)) Directory.Delete(_signalDirectory, true);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Target() => 1;

        [UnityTest]
        public IEnumerator 送ったコードからHarmonyのPostfixを適用して解除できる() => UniTask.ToCoroutine(async () =>
        {
            const string code = @"using HarmonyLib;
using System.Reflection;
var original = typeof(Client.Tests.RemoteExec.RemoteExecHarmonyTest).GetMethod(""Target"");
var postfix = typeof(RemoteExecSnippet).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
    .Single(method => method.Name.Contains(""g__Postfix|""));
var harmony = new Harmony(""remote-exec-harmony-test"");
var before = Client.Tests.RemoteExec.RemoteExecHarmonyTest.Target();
var during = 0;
// 失敗時もパッチを解除
// Remove the patch even on failure
try
{
    harmony.Patch(original, postfix: new HarmonyMethod(postfix));
    during = Client.Tests.RemoteExec.RemoteExecHarmonyTest.Target();
}
finally
{
    harmony.UnpatchAll(""remote-exec-harmony-test"");
}
var after = Client.Tests.RemoteExec.RemoteExecHarmonyTest.Target();
return $""{before},{during},{after}"";

static void Postfix(ref int __result) { __result = 42; }";

            // 差し込みと後片付けを確認
            // Verify the detour and cleanup
            var result = await RemoteExecRunner.RunAsync(code, RemoteExecTarget.Client, System.Threading.CancellationToken.None);
            Assert.AreEqual(RemoteExecOutcome.Succeeded, result.Outcome, result.Exception + string.Join("\n", result.CompileErrors));
            Assert.AreEqual("1,42,1", result.Result);
            Assert.AreEqual(1, Target());
        });
    }
}
