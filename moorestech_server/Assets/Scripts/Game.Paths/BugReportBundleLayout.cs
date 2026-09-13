namespace Game.Paths
{
    // バグ報告バンドル1箱の中の置き場の名前。書き側（クライアント）と読み側（再現ツール）が同じ名前を見るための唯一の定義元
    // The directory names inside one bug-report bundle; the single definition both the writer (client) and the reader (reproduction tools) look at
    // 片方だけ改名してもコンパイルは通り、書き出しは成功したまま再現だけが全滅するため、名前をここへ集約する
    // A one-sided rename still compiles and still writes successfully while only the reproduction dies, so the names live here
    public static class BugReportBundleLayout
    {
        public const string SnapshotDirectoryName = "snapshots";
        public const string WorldDirectoryName = "world";
        public const string ManifestFileName = "manifest.json";
        public const string RepositoryDirectoryName = "repo";
        public const string LogsDirectoryName = "logs";
        public const string UnityLogFileName = "unity.log";
        public const string VideoFileName = "video.mp4";
        public const string FramesDirectoryName = "frames";
        public const string FrameTicksFileName = "frames.tsv";
        public const string ScreenshotFileName = "screenshot.png";

        // 前回異常終了の箱だけが持つ置き場。録画は結合前の区間のまま、ダンプはOS生成のファイルのまま入る
        // Places only the previous-crash box has: the recording stays as unconcatenated segments and the dumps as the OS wrote them
        public const string RecordingDirectoryName = "recording";
        public const string CrashDumpsDirectoryName = "crashDumps";
    }
}
