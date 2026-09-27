using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace Client.RemoteExec.Compile
{
    // 読み込み済み DLL とランタイムのファサードを参照候補にする
    // Reference loaded DLLs and runtime facades for submitted code
    internal static class RemoteExecReferenceSet
    {
        internal static IReadOnlyList<MetadataReference> Collect(IEnumerable<Assembly> loadedAssemblies)
        {
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in loadedAssemblies)
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location)) continue;
                var name = assembly.GetName().Name;
                // UnityCliLoop.* はEditor専用ツールの内部コピーで、製品には存在せず公開型が重複する
                // UnityCliLoop.* are editor-tool copies absent from players and duplicate public types
                if (!ShouldIncludeAssemblyName(name)) continue;
                Add(name, assembly.Location);
            }

            // ランタイムDLLを補完する。別置きHarmonyは成功ロード済みの集合だけから得る
            // Supplement runtime DLLs; separate Harmony enters only through successfully loaded assemblies
            var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
            foreach (var path in Directory.GetFiles(runtimeDirectory, "*.dll")) Add(Path.GetFileNameWithoutExtension(path), path);
            var facadesDirectory = Path.Combine(runtimeDirectory, "Facades");
            if (Directory.Exists(facadesDirectory))
            {
                foreach (var path in Directory.GetFiles(facadesDirectory, "*.dll")) Add(Path.GetFileNameWithoutExtension(path), path);
            }
            return byName.Values.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();

            #region Internal

            void Add(string name, string path)
            {
                // Editorでは同名の別版が並ぶため、最初の参照を保って型の曖昧さを防ぐ
                // Keep the first version in the Editor to avoid ambiguous type references
                if (!byName.ContainsKey(name)) byName.Add(name, path);
            }

            #endregion
        }

        internal static bool ShouldIncludeAssemblyName(string name)
        {
            return !name.StartsWith("UnityCliLoop.", StringComparison.Ordinal);
        }
    }
}
