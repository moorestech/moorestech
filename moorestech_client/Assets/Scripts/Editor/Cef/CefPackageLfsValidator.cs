using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Client.Editor
{
    [InitializeOnLoad]
    public static class CefPackageLfsValidator
    {
        private const string PackageDirectoryPattern = "jp.juha.cefunity@*";
        private const string LfsPointerHeader = "version https://git-lfs";
        private const string ReportedSessionKey = "Moorestech.CefPackageLfsValidator.Reported";
        private const int MaximumPointerFileSize = 1024;

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
                    var fileInfo = new FileInfo(filePath);
                    if (MaximumPointerFileSize < fileInfo.Length) continue;

                    // 小さいファイルの先頭だけ読みます。
                    // Read only the prefix of small files.
                    using var stream = File.OpenRead(filePath);
                    var prefixLength = (int)System.Math.Min(stream.Length, LfsPointerHeader.Length);
                    var prefix = new byte[prefixLength];
                    var bytesRead = stream.Read(prefix, 0, prefix.Length);
                    if (Encoding.ASCII.GetString(prefix, 0, bytesRead) != LfsPointerHeader) continue;

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
