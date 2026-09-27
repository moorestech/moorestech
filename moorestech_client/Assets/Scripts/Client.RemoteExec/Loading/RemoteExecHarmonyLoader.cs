using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Client.RemoteExec.Loading
{
    internal static class RemoteExecHarmonyLoader
    {
        private static Assembly _harmony;
        private static bool _resolverRegistered;

        internal static void Load()
        {
            if (!_resolverRegistered)
            {
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                _resolverRegistered = true;
            }
            if (_harmony != null) return;

            // Editorのuloop同梱版を避け、名前が完全一致する版を使う
            // Avoid the editor's uloop copy and match the exact assembly name
            if (Application.isEditor)
            {
                _harmony = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "0Harmony");
                if (_harmony == null) Debug.LogWarning("[RemoteExec] 0Harmony が未読込です。Harmony無しで遠隔実行を続けます");
                return;
            }

            // 配布DLLはディスク境界。欠損・破損をログへ出して機能欠損を明示する
            // The shipped DLL is a disk boundary; report missing or damaged Harmony explicitly
            var path = Path.Combine(Application.dataPath, "RemoteExec", "0Harmony.dll");
            try
            {
                _harmony = Assembly.LoadFrom(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is BadImageFormatException)
            {
                Debug.LogWarning($"[RemoteExec] Harmony を読み込めません（Harmony無しで続行）: {e.Message}");
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            return new AssemblyName(args.Name).Name == "0Harmony" ? _harmony : null;
        }
    }
}
