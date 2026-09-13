using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration
{
    /// <summary>連鎖の結果。ロード可否と理由を同じ値で運び、呼び出し側にbool判定を残さない</summary>
    /// <summary>The chain's outcome, carrying loadability and its reason together so callers keep no bool logic</summary>
    public sealed class SaveMigrationResult
    {
        public bool CanLoad { get; }
        public string BlockedReason { get; }
        public int FromVersion { get; }
        public bool Migrated { get; }
        public JObject Save { get; }

        private SaveMigrationResult(bool canLoad, string blockedReason, int fromVersion, bool migrated, JObject save)
        {
            CanLoad = canLoad;
            BlockedReason = blockedReason;
            FromVersion = fromVersion;
            Migrated = migrated;
            Save = save;
        }

        public static SaveMigrationResult Blocked(int fromVersion, string reason)
        {
            return new SaveMigrationResult(false, reason, fromVersion, false, null);
        }

        public static SaveMigrationResult Completed(int fromVersion, bool migrated, JObject save)
        {
            return new SaveMigrationResult(true, null, fromVersion, migrated, save);
        }
    }
}
