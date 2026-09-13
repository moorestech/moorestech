using Game.SaveLoad.Interface;

namespace Game.SaveLoad.Migration
{
    /// <summary>ロード直前まで整えたセーブ。ロード不可のときは理由だけを運ぶ</summary>
    /// <summary>A save prepared up to the moment of load; when it cannot load it carries only the reason</summary>
    public sealed class PreparedSaveJson
    {
        public bool CanLoad { get; }
        public string BlockedReason { get; }
        public string SaveJsonText { get; }
        public MissingMasterPruneReport Report { get; }

        private PreparedSaveJson(bool canLoad, string blockedReason, string saveJsonText, MissingMasterPruneReport report)
        {
            CanLoad = canLoad;
            BlockedReason = blockedReason;
            SaveJsonText = saveJsonText;
            Report = report;
        }

        public static PreparedSaveJson Blocked(string reason)
        {
            return new PreparedSaveJson(false, reason, null, MissingMasterPruneReport.None);
        }

        public static PreparedSaveJson Ready(string saveJsonText, MissingMasterPruneReport report)
        {
            return new PreparedSaveJson(true, null, saveJsonText, report);
        }
    }
}
