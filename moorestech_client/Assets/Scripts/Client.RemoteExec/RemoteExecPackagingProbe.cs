using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityEngine;

namespace Client.RemoteExec
{
    public static class RemoteExecPackagingProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Probe()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-remote-exec-probe")) return;

            // 読み込み済みアセンブリだけを参照にして最小コードを emit する
            // Emit a minimal snippet referencing only loaded assemblies
            var refs = System.AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location)).Select(a => MetadataReference.CreateFromFile(a.Location));
            var tree = CSharpSyntaxTree.ParseText("public static class P { public static int Run() => 40 + 2; }");
            var compilation = CSharpCompilation.Create("probe", new[] { tree }, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream);

            var args = System.Environment.GetCommandLineArgs();
            var pathOptionIndex = System.Array.IndexOf(args, "-remote-exec-harmony-path");
            var harmonyPatched = "skipped";
            if (pathOptionIndex >= 0 && pathOptionIndex + 1 < args.Length)
            {
                // 外部 DLL の読み込みと反射呼び出しは失敗し得る境界なので、例外全文を結果に残す
                // Loading an external DLL and invoking it by reflection is a failure boundary, so retain the full exception in the result
                try
                {
                    var harmonyAssembly = Assembly.LoadFrom(args[pathOptionIndex + 1]);
                    var harmonyType = harmonyAssembly.GetType("HarmonyLib.Harmony", true);
                    var harmonyMethodType = harmonyAssembly.GetType("HarmonyLib.HarmonyMethod", true);
                    var harmony = System.Activator.CreateInstance(harmonyType, "probe");
                    var targetMethod = typeof(RemoteExecPackagingProbe).GetMethod(nameof(Target), BindingFlags.NonPublic | BindingFlags.Static);
                    var postfixMethod = typeof(RemoteExecPackagingProbe).GetMethod(nameof(Postfix), BindingFlags.NonPublic | BindingFlags.Static);
                    var postfix = System.Activator.CreateInstance(harmonyMethodType, new object[] { postfixMethod });
                    var patchMethod = harmonyType.GetMethods().Single(method => method.Name == "Patch" && method.GetParameters().Length == 5);
                    patchMethod.Invoke(harmony, new object[] { targetMethod, null, postfix, null, null });
                    try
                    {
                        harmonyPatched = (Target() == 99).ToString();
                    }
                    finally
                    {
                        harmonyType.GetMethod("UnpatchAll", new[] { typeof(string) }).Invoke(harmony, new object[] { "probe" });
                    }
                }
                catch (System.Exception e)
                {
                    harmonyPatched = $"error:{e}";
                }
            }

            Debug.Log($"[RemoteExecProbe] emit={emit.Success} diagnostics={string.Join(" | ", emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))} harmonyPatched={harmonyPatched}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Target() => 1;

        private static void Postfix(ref int __result)
        {
            __result = 99;
        }
    }
}
