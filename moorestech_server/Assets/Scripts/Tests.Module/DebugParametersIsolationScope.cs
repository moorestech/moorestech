using System;
using System.IO;
using Common.Debug;

namespace Tests.Module
{
    /// <summary>
    ///     テストアセンブリ実行中だけデバッグ設定をOS一時領域へ隔離する。
    ///     開発者の ../cache を読まないので、全テストがデバッグ設定の既定値から開始する。
    ///     Isolates debug parameters into an OS temp directory for the duration of a test assembly run.
    ///     The developer's ../cache is never read, so every test starts from default debug parameter values.
    /// </summary>
    public sealed class DebugParametersIsolationScope
    {
        // 隔離ディレクトリは必ずこの直下に作る。後始末時に「隔離先かどうか」をパスだけで判定するため
        // Every isolation directory lives here, so cleanup can tell isolation paths from real ones by path alone
        private static string IsolationRoot => Path.Combine(Path.GetTempPath(), "moorestech-debug-cache");

        private readonly string _isolatedCacheDirectory;
        private readonly string _priorOverride;

        private DebugParametersIsolationScope(string isolatedCacheDirectory, string priorOverride)
        {
            _isolatedCacheDirectory = isolatedCacheDirectory;
            _priorOverride = priorOverride;
        }

        public static DebugParametersIsolationScope Begin(string label)
        {
            // 実行毎にユニークな空ディレクトリを作る。異常終了で残っても一時領域なのでOSが回収する
            // Create a unique empty directory per run; an orphan left by an abnormal exit is reclaimed by the OS
            var directory = Path.Combine(IsolationRoot, $"{label}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            var priorOverride = DebugParametersCacheDirectory.GetOverride();
            DebugParametersCacheDirectory.SetOverride(directory);
            return new DebugParametersIsolationScope(directory, priorOverride);
        }

        public void End()
        {
            // 隔離先へは決して復元しない。ドメインリロードでBeginが重複しても最後は必ず解除に収束する
            // Never restore into an isolation directory, so duplicated Begins from a domain reload still converge to cleared
            DebugParametersCacheDirectory.SetOverride(IsIsolationDirectory(_priorOverride) ? null : _priorOverride);
            DeleteIfIsolationDirectory(_isolatedCacheDirectory);
        }

        /// <summary>
        ///     ドメインリロードでスコープ実体が失われた場合の後始末。環境変数から隔離先を割り出して解除する。
        ///     解除しないと消えた一時ディレクトリを指したままになり、以降の手動プレイの設定変更が無言で捨てられる。
        ///     Cleanup for when a domain reload loses the scope object; recovers the isolation target from the env var.
        ///     Without it the override would point at a deleted temp directory and silently discard later manual play settings.
        /// </summary>
        public static void EndOrphaned()
        {
            var currentOverride = DebugParametersCacheDirectory.GetOverride();
            if (!IsIsolationDirectory(currentOverride)) return;

            DebugParametersCacheDirectory.SetOverride(null);
            DeleteIfIsolationDirectory(currentOverride);
        }

        private static bool IsIsolationDirectory(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith(IsolationRoot, StringComparison.Ordinal);
        }

        private static void DeleteIfIsolationDirectory(string path)
        {
            if (IsIsolationDirectory(path) && Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
