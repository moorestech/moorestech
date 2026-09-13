namespace Game.SaveLoad.Migration
{
    /// <summary>ロード直前まで整えたセーブ。ロード不可のときは理由だけを運ぶ</summary>
    /// <summary>A save prepared up to the moment of load; when it cannot load it carries only the reason</summary>
    public sealed class PreparedSaveJson
    {
        public bool CanLoad { get; }
        public string BlockedReason { get; }
        public string SaveJsonText { get; }

        private PreparedSaveJson(bool canLoad, string blockedReason, string saveJsonText)
        {
            CanLoad = canLoad;
            BlockedReason = blockedReason;
            SaveJsonText = saveJsonText;
        }

        public static PreparedSaveJson Blocked(string reason)
        {
            return new PreparedSaveJson(false, reason, null);
        }

        public static PreparedSaveJson Ready(string saveJsonText)
        {
            return new PreparedSaveJson(true, null, saveJsonText);
        }
    }
}
