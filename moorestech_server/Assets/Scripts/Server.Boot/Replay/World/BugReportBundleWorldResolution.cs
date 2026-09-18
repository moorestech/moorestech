using Game.Paths;

namespace Server.Boot.Replay.World
{
    public enum BugReportBundleWorldOutcome
    {
        Resolved,
        Rejected,
    }

    // 解決か拒否かは列挙型一本で判別する。World と Reason を null で使い分けない
    // Resolved vs rejected is told by the enum alone; World and Reason are never distinguished by null
    public abstract class BugReportBundleWorldResolution
    {
        public abstract BugReportBundleWorldOutcome Outcome { get; }

        public sealed class ResolvedWorld : BugReportBundleWorldResolution
        {
            public readonly WorldDataDirectory World;
            internal ResolvedWorld(WorldDataDirectory world) { World = world; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Resolved;
        }

        public sealed class RejectedWorld : BugReportBundleWorldResolution
        {
            public readonly string Reason;
            internal RejectedWorld(string reason) { Reason = reason; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Rejected;
        }

        // 外から不整合な結果を組み立てられないよう、生成は同じアセンブリの factory だけに絞る
        // Construction stays behind these internal factories so only this assembly can assemble a result
        internal static BugReportBundleWorldResolution Resolved(WorldDataDirectory world) { return new ResolvedWorld(world); }
        internal static BugReportBundleWorldResolution Rejected(string reason) { return new RejectedWorld(reason); }
    }
}
