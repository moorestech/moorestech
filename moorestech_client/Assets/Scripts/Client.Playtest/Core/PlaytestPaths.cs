using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Client.Playtest.Core
{
    /// <summary>
    ///     プレイテスト成果物（readyマーカー・結果JSON・スクショ・録画）の出力先を一元管理する
    ///     Centralizes output paths for playtest artifacts (ready marker, result JSON, screenshots, recordings)
    /// </summary>
    public static class PlaytestPaths
    {
        private const string SessionDirKey = "Playtest_SessionDirectory";

        // 出力ルートはプロジェクト直下（Assets外なのでインポート対象にならない）
        // Output root sits at the project root (outside Assets, so Unity never imports it)
        public static string RootDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "../PlaytestResults"));

        // セッションディレクトリはSessionState保持なのでドメインリロードを跨いで参照できる
        // The session directory survives domain reloads because it is stored in SessionState
        public static string SessionDirectory => SessionState.GetString(SessionDirKey, string.Empty);

        public static string ReadyMarkerPath => Path.Combine(SessionDirectory, "ready.marker");

        // デバッグ設定の隔離先。セッション毎に作り直すので前回実行の残骸を読まない
        // Isolated debug parameter cache; recreated per session so a previous run's leftovers are never read
        public static string DebugCacheDirectory => string.IsNullOrEmpty(SessionDirectory) ? string.Empty : Path.Combine(SessionDirectory, "debug-cache");

        public static void ResetSession()
        {
            // タイムスタンプ付きセッションディレクトリを新規作成して記録する
            // Create and register a fresh timestamped session directory
            var directory = Path.Combine(RootDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(directory);
            SessionState.SetString(SessionDirKey, directory);
        }

        public static void WriteReadyMarker()
        {
            if (string.IsNullOrEmpty(SessionDirectory)) return;
            File.WriteAllText(ReadyMarkerPath, DateTime.Now.ToString("O"));
        }

        public static string CreateRunDirectory(string runName)
        {
            // セッション未初期化の単発実行はadhoc配下に逃がす
            // Ad-hoc runs without a session fall back to the adhoc directory
            var baseDirectory = string.IsNullOrEmpty(SessionDirectory) ? Path.Combine(RootDirectory, "adhoc") : SessionDirectory;
            var directory = Path.Combine(baseDirectory, runName);
            Directory.CreateDirectory(directory);

            // 再実行時に前回のresult.jsonをCLI側が誤回収しないよう先に消す
            // Delete a stale result.json first so the CLI never collects the previous run's result
            var staleResultPath = Path.Combine(directory, "result.json");
            if (File.Exists(staleResultPath)) File.Delete(staleResultPath);
            return directory;
        }
    }
}
