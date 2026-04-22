using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Client.WebUiHost.Boot
{
    /// <summary>
    /// moorestech_web/ 配下の絶対パスを解決するユーティリティ
    /// Resolves absolute paths under moorestech_web/
    /// </summary>
    public static class WebUiPaths
    {
        // エディタ実行時: moorestech_client/Assets の2階層上 = リポジトリルート
        // At editor time: two levels up from moorestech_client/Assets == repo root
        // Application.dataPath は moorestech_client/Assets を指すので、
        // その親（moorestech_client）の親がリポジトリルート。
        // Application.dataPath points at moorestech_client/Assets,
        // so its parent's parent is the repo root.
        public static string RepoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static string WebRoot =>
            Path.Combine(RepoRoot, "moorestech_web");

        public static string WebuiRoot =>
            Path.Combine(WebRoot, "webui");

        public static string NodeBinary
        {
            get
            {
                var platform = GetPlatformDir();
                // Windows は node.exe、それ以外は bin/node
                // Windows: node.exe, others: bin/node
                var rel = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "node.exe"
                    : Path.Combine("bin", "node");
                return Path.Combine(WebRoot, "node", platform, rel);
            }
        }

        public static string PnpmBinary
        {
            get
            {
                var platform = GetPlatformDir();
                var file = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "pnpm.exe" : "pnpm";
                return Path.Combine(WebRoot, "node", platform, file);
            }
        }

        // プラットフォーム別ディレクトリ名（setup.sh / setup.ps1 と一致）
        // Per-platform directory name (matches setup.sh / setup.ps1)
        public static string GetPlatformDir()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "mac-arm64" : "mac-x64";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux-x64";
            }
            return "unknown";
        }
    }
}
