using Game.Paths;

namespace Client.PlaytestReceiver.Upload.Attempt
{
    // 宣言に載せる順。件数・総量の上限は先頭から埋まるので、再現に要るものを先に、静止画を最後に置く
    // The order files enter the declaration; the count/total caps fill from the front, so what reproduction needs goes first and the stills last
    internal enum PlaytestBundleFileRank
    {
        Required = 0,
        Supporting = 1,
        Frames = 2,
    }

    // 箱内パスの優先度。名前はバンドルのレイアウト定数だけから引き、ここで文字列を再定義しない
    // The priority of a path inside a box; names come only from the bundle layout constants, never redefined here
    internal static class PlaytestBundleFilePriority
    {
        // manifest・world/・snapshots/ は再現の土台。欠けた箱は届いても ReplayCheck が必ず Reject する
        // manifest, world/ and snapshots/ are the reproduction's foundation; a box lacking them always gets rejected by ReplayCheck
        public static PlaytestBundleFileRank RankOf(string relativePath)
        {
            if (relativePath == BugReportBundleLayout.ManifestFileName) return PlaytestBundleFileRank.Required;
            var firstSegment = FirstDirectorySegment(relativePath);
            if (firstSegment == BugReportBundleLayout.WorldDirectoryName || firstSegment == BugReportBundleLayout.SnapshotDirectoryName) return PlaytestBundleFileRank.Required;
            if (firstSegment == BugReportBundleLayout.FramesDirectoryName) return PlaytestBundleFileRank.Frames;
            return PlaytestBundleFileRank.Supporting;
        }

        public static bool IsRequired(string relativePath)
        {
            return RankOf(relativePath) == PlaytestBundleFileRank.Required;
        }

        // 直下のファイル（frames.tsv等）はディレクトリ名と一致させない
        // A root-level file (frames.tsv and the like) never matches a directory name
        private static string FirstDirectorySegment(string relativePath)
        {
            var slash = relativePath.IndexOf('/');
            return slash < 0 ? "" : relativePath.Substring(0, slash);
        }
    }
}
