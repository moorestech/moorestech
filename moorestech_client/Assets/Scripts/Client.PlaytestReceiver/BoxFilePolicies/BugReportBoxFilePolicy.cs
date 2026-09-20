using Client.PlaytestReceiver.Upload.Attempt;
using Game.Paths;

namespace Client.PlaytestReceiver.BoxFilePolicies
{
    // バグ報告の箱の格付け。名前はバンドルのレイアウト定数だけから引き、ここで文字列を再定義しない
    // Ranks paths in a bug report box; names come only from the bundle layout constants, never redefined here
    public sealed class BugReportBoxFilePolicy : IPlaytestBoxFilePolicy
    {
        // manifest・world/・snapshots/ は再現の土台。欠けた箱は届いても ReplayCheck が必ず Reject する
        // manifest, world/ and snapshots/ are the reproduction's foundation; a box lacking them always gets rejected by ReplayCheck
        public PlaytestBundleFileRank RankOf(string relativePath)
        {
            if (relativePath == BugReportBundleLayout.ManifestFileName) return PlaytestBundleFileRank.Required;
            var firstSegment = FirstDirectorySegment();
            if (firstSegment == BugReportBundleLayout.WorldDirectoryName || firstSegment == BugReportBundleLayout.SnapshotDirectoryName) return PlaytestBundleFileRank.Required;
            if (firstSegment == BugReportBundleLayout.FramesDirectoryName) return PlaytestBundleFileRank.Frames;
            return PlaytestBundleFileRank.Supporting;

            #region Internal

            // 直下のファイル（frames.tsv等）はディレクトリ名と一致させない
            // A root-level file (frames.tsv and the like) never matches a directory name
            string FirstDirectorySegment()
            {
                var slash = relativePath.IndexOf('/');
                return slash < 0 ? "" : relativePath.Substring(0, slash);
            }

            #endregion
        }
    }
}
