using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Recording
{
    public static class FfmpegLocator
    {
        public const string MissingFfmpegReason = "ffmpeg が見つかりません（配布物同梱 moorestech_Data/Plugins/x86_64/ffmpeg.exe・MOORESTECH_FFMPEG・PATH・既知の場所 /opt/homebrew/bin, /usr/local/bin のいずれにも無い）";
        // 同梱ffmpeg実行ファイル名
        // The bundled ffmpeg executable name
        public const string BundledWindowsExecutableName = "ffmpeg.exe";

        private static readonly string[] KnownPaths = { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg" };

        // 同梱→環境変数→PATH→既知の場所の順に探す。無ければ null（呼び出し側が縮退を記録する）
        // Search the bundled copy, then env var, then PATH, then known locations; null if absent (the caller records the degradation)
        public static string Find()
        {
            var bundledPath = Path.Combine(Application.dataPath, "Plugins", "x86_64", BundledWindowsExecutableName);
            var pathExecutableName = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer
                ? "ffmpeg.exe"
                : "ffmpeg";
            return FindIn(bundledPath, global::System.Environment.GetEnvironmentVariable("MOORESTECH_FFMPEG"), global::System.Environment.GetEnvironmentVariable("PATH"), pathExecutableName, KnownPaths);
        }

        // 見つからなかったときの縮退理由。無音で諦めず理由を残し、報告側が欠損として記録できるようにする
        // The degradation reason when ffmpeg is absent; never fail silently so the report can record the gap
        public static RecordingAvailability ResolveInitialAvailability(string ffmpegPath)
        {
            if (ffmpegPath != null) return RecordingAvailability.Available();
            UnityEngine.Debug.LogWarning($"録画リングを開始しません: {MissingFfmpegReason}");
            return RecordingAvailability.Unavailable(MissingFfmpegReason);
        }

        // 探索元を引数で受ける版。ffmpegが無い環境の挙動をテストで固定するために公開している
        // Takes the search sources as arguments so tests can pin the behaviour of a machine without ffmpeg
        // pathExecutableNameも呼び出し側から渡す純粋な関数にする。Application.platformの読み取りをスレッド制約から切り離すため
        // pathExecutableName is passed in too, keeping this a pure function decoupled from Application.platform's thread constraint
        public static string FindIn(string bundledPath, string overridePath, string pathVariable, string pathExecutableName, IReadOnlyList<string> knownPaths)
        {
            if (!string.IsNullOrEmpty(bundledPath) && File.Exists(bundledPath)) return bundledPath;
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) return overridePath;

            foreach (var directory in (pathVariable ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(directory)) continue;
                var candidate = Path.Combine(directory, pathExecutableName);
                if (File.Exists(candidate)) return candidate;
            }
            foreach (var candidate in knownPaths)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
