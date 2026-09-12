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
    }
}
