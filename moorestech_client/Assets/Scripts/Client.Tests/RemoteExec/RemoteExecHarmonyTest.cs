using System.Collections;
using System.Runtime.CompilerServices;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecHarmonyTest
    {
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
// 適用後に呼び出し、失敗してもパッチを解除する
// Call after patching and always remove the patch on failure
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

            // 前後の値も見て、実際の差し込み効果と後片付けを確かめる
            // Check before and after values to verify the detour and cleanup
            var result = await RemoteExecRunner.RunAsync(code, RemoteExecTarget.Client);
            Assert.IsTrue(result.Ok, result.Exception + string.Join("\n", result.CompileErrors));
            Assert.AreEqual("1,42,1", result.Result);
            Assert.AreEqual(1, Target());
        });
    }
}
