using System.Collections.Generic;
using System.IO;

namespace Client.Game.InGame.BugReport.Recording
{
    public static class FfmpegLocator
    {
        public const string MissingFfmpegReason = "ffmpeg が見つかりません（MOORESTECH_FFMPEG か PATH で指定）";

        private static readonly string[] KnownPaths = { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg" };

        // 環境変数→PATH→既知の場所の順に探す。無ければ null（呼び出し側が縮退を記録する）
        // Search env var, then PATH, then known locations; null if absent (the caller records the degradation)
        public static string Find()
        {
            return FindIn(global::System.Environment.GetEnvironmentVariable("MOORESTECH_FFMPEG"), global::System.Environment.GetEnvironmentVariable("PATH"), KnownPaths);
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
        public static string FindIn(string overridePath, string pathVariable, IReadOnlyList<string> knownPaths)
        {
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) return overridePath;

            foreach (var directory in (pathVariable ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(directory)) continue;
                var candidate = Path.Combine(directory, "ffmpeg");
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
