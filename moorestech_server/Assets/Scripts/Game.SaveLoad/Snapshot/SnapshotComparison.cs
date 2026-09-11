using System.Collections.Generic;

namespace Game.SaveLoad.Snapshot
{
    // スナップショット2枚の比較結果。差が1件も無いときだけ一致とみなす
    // The result of comparing two snapshots; equal only when no difference was found
    public sealed class SnapshotComparison
    {
        public bool Equal => Differences.Count == 0;
        public IReadOnlyList<string> Differences { get; }

        public SnapshotComparison(IReadOnlyList<string> differences)
        {
            Differences = differences;
        }
    }
}
