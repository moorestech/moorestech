using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration
{
    /// <summary>連鎖の結果。ロード可否と理由を同じ値で運び、呼び出し側にbool判定を残さない</summary>
    /// <summary>The chain's outcome, carrying loadability and its reason together so callers keep no bool logic</summary>
    public sealed class SaveMigrationResult
    {
        public bool CanLoad { get; }

        // 原因は分岐と表示の正本、理由文は開発者がログで読むための詳細
        // The cause is the source of truth for branching and display; the reason text is detail for developers reading logs
        public SaveLoadBlockedCause? BlockedCause { get; }
        public string BlockedReason { get; }
        public int FromVersion { get; }
        public bool Migrated { get; }
        public JObject Save { get; }

        private SaveMigrationResult(bool canLoad, SaveLoadBlockedCause? blockedCause, string blockedReason, int fromVersion, bool migrated, JObject save)
        {
            CanLoad = canLoad;
            BlockedCause = blockedCause;
            BlockedReason = blockedReason;
            FromVersion = fromVersion;
            Migrated = migrated;
            Save = save;
        }

        public static SaveMigrationResult Blocked(int fromVersion, SaveLoadBlockedCause cause, string reason)
        {
            return new SaveMigrationResult(false, cause, reason, fromVersion, false, null);
        }

        public static SaveMigrationResult Completed(int fromVersion, bool migrated, JObject save)
        {
            return new SaveMigrationResult(true, null, null, fromVersion, migrated, save);
        }
    }
}
