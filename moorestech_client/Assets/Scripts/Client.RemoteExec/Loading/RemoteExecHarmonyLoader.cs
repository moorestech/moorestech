using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Client.RemoteExec.Loading
{
    // Editor専用asmdefを持たないRemoteExecHarmonyBundlerから定数を参照させるためpublicにする（FfmpegLocator前例）
    // Public so the asmdef-less RemoteExecHarmonyBundler (Editor) can reference the constants, matching the FfmpegLocator precedent
    public static class RemoteExecHarmonyLoader
    {
        // Harmonyアセンブリ名。ビルド側の同梱・実行時の探索・アセンブリ解決で共有する
        // The Harmony assembly name shared by the build-side bundling, runtime lookup and assembly resolution
        public const string HarmonyAssemblyName = "0Harmony";
        // 同梱先の相対ディレクトリ（Application.dataPath基準）。ビルド側の同梱先と実行時の探索先で共有する
        // The bundled directory relative to Application.dataPath, shared by the build bundler and the runtime lookup
        public const string BundledRelativeDirectory = "RemoteExec";
        public const string BundledFileName = HarmonyAssemblyName + ".dll";

        private static Assembly _harmony;
        private static bool _resolverRegistered;
        internal static string LoadFailureReason { get; private set; }

        internal static void Load()
        {
            if (!_resolverRegistered)
            {
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
                _resolverRegistered = true;
            }
            if (_harmony != null)
            {
                LoadFailureReason = null;
                return;
            }

            // Editorのuloop同梱版を避け、名前が完全一致する版を使う
            // Avoid the editor's uloop copy and match the exact assembly name
            if (Application.isEditor)
            {
                _harmony = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == HarmonyAssemblyName);
                if (_harmony == null)
                {
                    LoadFailureReason = "0Harmony がEditorに読み込まれていません";
                    Debug.LogWarning($"[RemoteExec] {LoadFailureReason}。Harmony無しで遠隔実行を続けます");
                }
                else LoadFailureReason = null;
                return;
            }

            // 配布DLLはディスク境界。欠損・破損をログへ出して機能欠損を明示する
            // The shipped DLL is a disk boundary; report missing or damaged Harmony explicitly
            var path = Path.Combine(Application.dataPath, BundledRelativeDirectory, BundledFileName);
            try
            {
                _harmony = Assembly.LoadFrom(path);
                LoadFailureReason = null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is BadImageFormatException)
            {
                LoadFailureReason = $"Harmony DLL を読み込めません ({path}): {e.Message}";
                Debug.LogWarning($"[RemoteExec] {LoadFailureReason}。Harmony無しで遠隔実行を続けます");
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            return new AssemblyName(args.Name).Name == HarmonyAssemblyName ? _harmony : null;
        }
    }
}
