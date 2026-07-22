using System;
using System.Collections.Generic;
using System.IO;

namespace Common.Debug
{
    /// <summary>
    ///     デバッグ設定JSONの置き場所を解決する。
    ///     テスト・自動プレイテストはここを差し替えることで、開発者の永続デバッグ設定から隔離される。
    ///     Resolves where the debug parameter JSON files live.
    ///     Tests and automated playtests swap it to stay isolated from the developer's persistent debug settings.
    /// </summary>
    public static class DebugParametersCacheDirectory
    {
        public const string BoolFileName = "BoolDebugParameters.json";
        public const string IntFileName = "IntDebugParameters.json";
        public const string StringFileName = "StringDebugParameters.json";

        public static readonly IReadOnlyList<string> ParameterFileNames = new[] { BoolFileName, IntFileName, StringFileName };

        private const string DefaultRelativePath = "../cache";
        private const string OverrideEnvironmentVariableName = "MOORESTECH_DEBUG_CACHE_DIR";

        /// <summary>
        ///     現在の解決先。上書きが無ければ既定の ../cache
        ///     The currently resolved directory; the default ../cache when no override is set
        /// </summary>
        public static string Resolve()
        {
            var overrideDirectory = GetOverride();
            return string.IsNullOrEmpty(overrideDirectory) ? GetDefault() : overrideDirectory;
        }

        public static string GetOverride()
        {
            return Environment.GetEnvironmentVariable(OverrideEnvironmentVariableName);
        }

        /// <summary>
        ///     解決先を差し替える。nullで解除。
        ///     プロセス環境変数なのでドメインリロードを跨いで維持され、クラッシュ・強制終了ではプロセスと共に消えるため残置事故が構造的に起きない。
        ///     Swaps the resolved directory (null clears it).
        ///     Being a process environment variable it survives domain reloads and dies with the process on crash, so a stale override can never be left behind.
        /// </summary>
        public static void SetOverride(string directoryPath)
        {
            var value = string.IsNullOrEmpty(directoryPath) ? null : directoryPath;
            Environment.SetEnvironmentVariable(OverrideEnvironmentVariableName, value);
        }

        /// <summary>
        ///     既定の解決先の内容を指定ディレクトリへ複製する（隔離環境に開発者設定を引き継がせる用途）
        ///     Copies the default directory's contents into the given directory (to carry developer settings into an isolated environment)
        /// </summary>
        public static void CopyDefaultTo(string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            var sourceDirectory = GetDefault();
            foreach (var fileName in ParameterFileNames)
            {
                var sourcePath = Path.Combine(sourceDirectory, fileName);
                if (!File.Exists(sourcePath)) continue;
                File.Copy(sourcePath, Path.Combine(destinationDirectory, fileName), true);
            }
        }

        public static string GetDefault()
        {
            return Path.GetFullPath(DefaultRelativePath);
        }
    }
}
