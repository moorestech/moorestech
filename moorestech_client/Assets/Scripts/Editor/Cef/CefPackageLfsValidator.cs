using System.IO;
using UnityEditor;
using UnityEngine;

namespace Client.Editor
{
    [InitializeOnLoad]
    public static class CefPackageLfsValidator
    {
        private const string PackageDirectoryPattern = "jp.juha.cefunity@*";
        private const string ReportedSessionKey = "Moorestech.CefPackageLfsValidator.Reported";

        static CefPackageLfsValidator()
        {
            // UPM解決後に検査します。
            // Validate after UPM resolution.
            EditorApplication.delayCall += ValidateResolvedPackage;
        }

        private static void ValidateResolvedPackage()
        {
            if (SessionState.GetBool(ReportedSessionKey, false)) return;

            var packageCachePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/PackageCache"));
            if (!Directory.Exists(packageCachePath)) return;

            // 各キャッシュのLFS残存を探します。
            // Find unresolved LFS files in each cache.
            foreach (var packageDirectory in Directory.EnumerateDirectories(packageCachePath, PackageDirectoryPattern))
            {
                if (!TryFindLfsPointer(packageDirectory, out var pointerPath)) continue;

                SessionState.SetBool(ReportedSessionKey, true);
                Debug.LogError(BuildRecoveryMessage(pointerPath));
                return;
            }

            #region Internal

            bool TryFindLfsPointer(string packageDirectory, out string pointerPath)
            {
                foreach (var filePath in Directory.EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories))
                {
                    if (!CefLfsPointer.IsPointerFile(filePath)) continue;

                    pointerPath = filePath;
                    return true;
                }

                pointerPath = string.Empty;
                return false;
            }

            string BuildRecoveryMessage(string pointerPath)
            {
                var relativePath = Path.GetRelativePath(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), pointerPath);

                // OS別の復旧方法を案内します。
                // Show recovery steps for each OS.
                return "CEF package contains an unresolved Git LFS pointer: " + relativePath + "\n" +
                       "Close Unity, run the repository setup script, then reopen the project so UPM resolves CEF again.\n" +
                       "macOS/Linux: ./scripts/setup-cef.sh\n" +
                       "Windows PowerShell: .\\scripts\\setup-cef.ps1";
            }

            #endregion
        }
    }
}
