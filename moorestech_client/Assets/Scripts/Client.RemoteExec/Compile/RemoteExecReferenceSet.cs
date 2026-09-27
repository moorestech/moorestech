using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using System.Reflection;

namespace Client.RemoteExec.Compile
{
    // DLL+ランタイムを参照候補にする
    // Reference DLLs + runtime facades
    internal static class RemoteExecReferenceSet
    {
        // ランタイム/Facadesはプロセス起動後は変わらないため、ディスク列挙とMetadataReference生成を1回だけ行う
        // Runtime/Facades never change after the process starts, so their disk enumeration and MetadataReference creation happen only once
        private static readonly object RuntimeFacadeCacheLock = new();
        private static IReadOnlyDictionary<string, MetadataReference> _runtimeFacadeCache;

        // パスからMetadataReferenceへのstaticキャッシュ。要求ごとに数百DLLを読み直さない
        // A static path-to-MetadataReference cache so hundreds of DLLs are never re-read per request
        private static readonly object PathReferenceCacheLock = new();
        private static readonly Dictionary<string, MetadataReference> PathReferenceCache = new(StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlyList<MetadataReference> Collect(IEnumerable<Assembly> loadedAssemblies)
        {
            var byName = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
            foreach (var assembly in loadedAssemblies)
            {
                if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location)) continue;
                var name = assembly.GetName().Name;
                // UnityCliLoop.* はEditor専用ツールの内部コピーで、製品には存在せず公開型が重複する
                // UnityCliLoop.* are editor-tool copies absent from players and duplicate public types
                if (!ShouldIncludeAssemblyName(name)) continue;
                Add(name, ReferenceForPath(assembly.Location));
            }

            // ランタイムDLLを補完する。別置きHarmonyは成功ロード済みの集合だけから得る
            // Supplement runtime DLLs; separate Harmony enters only through successfully loaded assemblies
            foreach (var pair in RuntimeFacadeReferences()) Add(pair.Key, pair.Value);
            return byName.Values.ToList();

            #region Internal

            void Add(string name, MetadataReference reference)
            {
                // Editorでは同名の別版が並ぶため、最初の参照を保って型の曖昧さを防ぐ
                // Keep the first version in the Editor to avoid ambiguous type references
                if (!byName.ContainsKey(name)) byName.Add(name, reference);
            }

            #endregion
        }

        internal static bool ShouldIncludeAssemblyName(string name)
        {
            return !name.StartsWith("UnityCliLoop.", StringComparison.Ordinal);
        }

        private static MetadataReference ReferenceForPath(string path)
        {
            lock (PathReferenceCacheLock)
            {
                if (PathReferenceCache.TryGetValue(path, out var cached)) return cached;
                var reference = MetadataReference.CreateFromFile(path);
                PathReferenceCache[path] = reference;
                return reference;
            }
        }

        private static IReadOnlyDictionary<string, MetadataReference> RuntimeFacadeReferences()
        {
            if (_runtimeFacadeCache != null) return _runtimeFacadeCache;
            lock (RuntimeFacadeCacheLock)
            {
                return _runtimeFacadeCache ??= BuildRuntimeFacadeReferences();
            }

            #region Internal

            Dictionary<string, MetadataReference> BuildRuntimeFacadeReferences()
            {
                var byName = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
                var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location);
                foreach (var path in Directory.GetFiles(runtimeDirectory, "*.dll")) AddPath(path);
                var facadesDirectory = Path.Combine(runtimeDirectory, "Facades");
                if (Directory.Exists(facadesDirectory))
                {
                    foreach (var path in Directory.GetFiles(facadesDirectory, "*.dll")) AddPath(path);
                }
                return byName;

                void AddPath(string path)
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (!byName.ContainsKey(name)) byName.Add(name, ReferenceForPath(path));
                }
            }

            #endregion
        }
    }
}
