using Game.Paths;

namespace Server.Boot.Replay.World
{
    // 解決か拒否かの結果。読み手は TryGetWorld 1本だけで分岐し、判別子や型の下調べを書き写さない
    // A resolved-or-rejected result; readers branch through TryGetWorld alone and never copy a discriminator check or a type test
    public sealed class BugReportBundleWorldResolution
    {
        private readonly bool _isResolved;
        private readonly WorldDataDirectory _world;
        private readonly string _rejectedReason;

        private BugReportBundleWorldResolution(bool isResolved, WorldDataDirectory world, string rejectedReason)
        {
            _isResolved = isResolved;
            _world = world;
            _rejectedReason = rejectedReason;
        }

        // 解決していれば world、拒否なら rejectedReason だけが意味を持つ
        // Only world is meaningful when resolved, and only rejectedReason when rejected
        public bool TryGetWorld(out WorldDataDirectory world, out string rejectedReason)
        {
            world = _world;
            rejectedReason = _rejectedReason;
            return _isResolved;
        }

        // 外から不整合な結果を組み立てられないよう、生成は同じアセンブリの factory だけに絞る
        // Construction stays behind these internal factories so only this assembly can assemble a result
        internal static BugReportBundleWorldResolution Resolved(WorldDataDirectory world) { return new BugReportBundleWorldResolution(true, world, null); }
        internal static BugReportBundleWorldResolution Rejected(string reason) { return new BugReportBundleWorldResolution(false, null, reason); }
    }
}
