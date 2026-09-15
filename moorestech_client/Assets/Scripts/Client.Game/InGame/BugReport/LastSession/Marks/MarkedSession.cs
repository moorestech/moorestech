namespace Client.Game.InGame.BugReport.LastSession
{
    // 印が置かれた1セッション。印は pid_<PID>/session_<utcTicks>/ で割られ、同じpidの再生し直しとも混ざらない
    // One session holding marks; the marks are split by pid_<PID>/session_<utcTicks>/, so a same-pid replay never mixes in
    public sealed class MarkedSession
    {
        public int ProcessId;
        public string SessionName;
    }

    // 1セッションぶんの印を消費した結果
    // The result of consuming one session's marks
    public sealed class SessionExitRecord
    {
        public bool ExitedCleanly;

        // 終了の意思は表明されたが書き出し完了の印が無い。終了処理の途中で止まった（フリーズ・強制終了）セッション
        // The intent to exit was declared but the flush-finished mark is absent: the session stopped midway through shutdown (a freeze or a forced kill)
        public bool ShutdownStalled;

        // セッション開始時に書いた出所。読めなければnullで理由が入る
        // The origin written at session start; null with a reason when unreadable
        public SessionOriginSnapshot Origin;
        public string OriginMissingReason;
    }
}
