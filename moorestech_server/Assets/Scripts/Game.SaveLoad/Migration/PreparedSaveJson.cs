using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration
{
    /// <summary>ロード直前まで整えたセーブ。ロード不可のときは原因と理由だけを運ぶ</summary>
    /// <summary>A save prepared up to the moment of load; when it cannot load it carries only the cause and reason</summary>
    public sealed class PreparedSaveJson
    {
        public bool CanLoad { get; }

        // 原因は分岐と表示の正本、理由文は開発者がログで読むための詳細
        // The cause is the source of truth for branching and display; the reason text is detail for developers reading logs
        public SaveLoadBlockedCause? BlockedCause { get; }
        public string BlockedReason { get; }
        public JObject Save { get; }

        private PreparedSaveJson(bool canLoad, SaveLoadBlockedCause? blockedCause, string blockedReason, JObject save)
        {
            CanLoad = canLoad;
            BlockedCause = blockedCause;
            BlockedReason = blockedReason;
            Save = save;
        }

        public static PreparedSaveJson Blocked(SaveLoadBlockedCause cause, string reason)
        {
            return new PreparedSaveJson(false, cause, reason, null);
        }

        public static PreparedSaveJson Ready(JObject save)
        {
            return new PreparedSaveJson(true, null, null, save);
        }
    }
}
