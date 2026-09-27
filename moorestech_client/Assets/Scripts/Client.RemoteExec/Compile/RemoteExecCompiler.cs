using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Client.RemoteExec.Compile
{
    internal sealed class RemoteExecCompileOutcome
    {
        public Assembly Assembly { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool Succeeded => Assembly != null;

        internal RemoteExecCompileOutcome(Assembly assembly, IReadOnlyList<string> errors)
        {
            Assembly = assembly;
            Errors = errors;
        }
    }

    // 本体コードを Roslyn でコンパイルして結果と診断を返す
    // Compile a method body with Roslyn and return the assembly or diagnostics
    internal static class RemoteExecCompiler
    {
        private static int _sequence;

        internal static RemoteExecCompileOutcome Compile(string body)
        {
            return CompileWithAssemblies(body, AppDomain.CurrentDomain.GetAssemblies());
        }

        internal static RemoteExecCompileOutcome CompileWithAssemblies(string body, Assembly[] loadedAssemblies)
        {
            var tree = CSharpSyntaxTree.ParseText(RemoteExecSourceWrapper.Wrap(body));
            var name = $"RemoteExecSnippet_{Interlocked.Increment(ref _sequence)}";
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);
            var compilation = CSharpCompilation.Create(name, new[] { tree }, RemoteExecReferenceSet.Collect(loadedAssemblies), options);

            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream);
            if (!emit.Success)
            {
                // #line 指定により診断位置は送信ソースの行番号を示す
                // The #line directive maps diagnostic locations to submitted source lines
                var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString()).ToList();
                return new RemoteExecCompileOutcome(null, errors);
            }
            return new RemoteExecCompileOutcome(Assembly.Load(stream.ToArray()), new List<string>());
        }
    }
}
