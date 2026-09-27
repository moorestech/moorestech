using System;
using System.Linq;
using System.Reflection;
using Client.RemoteExec;
using Client.RemoteExec.Compile;
using NUnit.Framework;

namespace Client.Tests.RemoteExec
{
    public class RemoteExecCompilerTest
    {
        [Test]
        public void 本体コードを包んでコンパイルし実行用の型が得られる()
        {
            var outcome = RemoteExecCompiler.Compile("return 1 + 1;");
            Assert.IsTrue(outcome.Succeeded, string.Join("\n", outcome.Errors));
            Assert.IsNotNull(outcome.Assembly.GetType(RemoteExecSourceWrapper.EntryTypeName));
        }

        [Test]
        public void 構文エラーは行番号付きの診断で返る()
        {
            var outcome = RemoteExecCompiler.Compile("return 1 +;");
            Assert.IsFalse(outcome.Succeeded);
            Assert.That(outcome.Errors.Single(), Does.Contain("(1,"));
        }

        [Test]
        public void 先頭のusing行は名前空間の指定として使われる()
        {
            var outcome = RemoteExecCompiler.Compile("using System.Text; // namespace\nreturn new StringBuilder(\"a\").ToString();");
            Assert.IsTrue(outcome.Succeeded, string.Join("\n", outcome.Errors));
        }

        [Test]
        public void return無しの本体もコンパイルできる()
        {
            var outcome = RemoteExecCompiler.Compile("UnityEngine.Debug.Log(\"x\");");
            Assert.IsTrue(outcome.Succeeded, string.Join("\n", outcome.Errors));
        }

        [Test]
        public void 先頭のusing宣言は本体として扱う()
        {
            var outcome = RemoteExecCompiler.Compile("using var stream = new System.IO.MemoryStream();\nreturn stream.Length;");
            Assert.IsTrue(outcome.Succeeded, string.Join("\n", outcome.Errors));
        }

        [Test]
        public void Harmony未読込でも一般コードは成功しHarmony利用だけ診断になる()
        {
            // Editorの読込状態に依存せず、DLL読込失敗後の参照集合を再現する
            // Reproduce references after a DLL load failure independently of the editor's loaded Harmony
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.GetName().Name.Contains("Harmony")).ToArray();
            var compile = typeof(RemoteExecCompiler).GetMethod("CompileWithAssemblies", BindingFlags.NonPublic | BindingFlags.Static);
            var ordinary = (RemoteExecCompileOutcome)compile.Invoke(null, new object[] { "return 1 + 1;", assemblies });
            Assert.IsTrue(ordinary.Succeeded, string.Join("\n", ordinary.Errors));

            var harmony = (RemoteExecCompileOutcome)compile.Invoke(null, new object[] { "return new HarmonyLib.Harmony(\"missing-harmony\");", assemblies });
            Assert.IsFalse(harmony.Succeeded);
            Assert.That(harmony.Errors, Has.Some.Contains("HarmonyLib"));
        }

        [Test]
        public void Editorツール内部の再同梱アセンブリは参照候補から外す()
        {
            var type = typeof(RemoteExecCompiler).Assembly.GetType("Client.RemoteExec.Compile.RemoteExecReferenceSet");
            var filter = type.GetMethod("ShouldIncludeAssemblyName", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsFalse((bool)filter.Invoke(null, new object[] { "UnityCliLoop.0Harmony" }));
            Assert.IsFalse((bool)filter.Invoke(null, new object[] { "UnityCliLoop.System.Reflection.Metadata" }));
            Assert.IsTrue((bool)filter.Invoke(null, new object[] { "0Harmony" }));
        }

        [Test]
        public void 起動オプションは完全一致したときだけ有効になる()
        {
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "game", "-remote-exec-extra" });
            Assert.IsFalse(RemoteExecLaunchOption.IsEnabled);
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "game", RemoteExecLaunchOption.Marker });
            Assert.IsTrue(RemoteExecLaunchOption.IsEnabled);
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "game" });
        }
    }
}
